using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK.Graphics;
using Yokko.Game.Screens;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneScreenTransitions : TestScene
{
    private DeferredScreenStack stack;
    private TransitionScreen first;
    private TransitionScreen second;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create navigation stack", () =>
        {
            Clear();
            Add(stack = new DeferredScreenStack());
            stack.Push(first = new TransitionScreen(Color4.CornflowerBlue));
        });
        AddUntilStep("first page entered", () => first.IsLoaded && first.Alpha == 1);
    }

    [Test]
    public void SourceStaysVisibleDuringLoadingAndDestinationReveal()
    {
        AddStep("hold destination loading", () =>
        {
            stack.DeferLoad = true;
            stack.Push(second = new TransitionScreen(Color4.Coral));
        });
        AddWaitStep("pending load", 3);
        AddAssert("old page remains opaque and alive", () => first.IsAlive && first.Alpha == 1);
        AddStep("complete loading", () => stack.ReleaseLoad());
        AddUntilStep("destination loaded", () => second.IsLoaded);
        AddAssert("coverage during fade", () => second.Alpha == 1 || (first.IsAlive && first.Alpha == 1));
        AddUntilStep("destination settled", () => second.Alpha == 1 && !first.IsAlive);
    }

    [Test]
    public void RapidBackCancelsEntryAndRestoresParent()
    {
        bool returnedDuringEntry = false;
        AddStep("push and return during fade", () =>
        {
            stack.Push(second = new TransitionScreen(Color4.Coral));
            second.OnUpdate += _ =>
            {
                if (returnedDuringEntry || second.Alpha <= 0 || second.Alpha >= 1)
                    return;
                returnedDuringEntry = true;
                second.Exit();
            };
        });
        AddUntilStep("returned before entry settled", () => returnedDuringEntry);
        AddAssert("parent restored", () => stack.CurrentScreen == first && first.Alpha == 1);
        AddUntilStep("outgoing page retired", () => !second.IsAlive);
        AddAssert("no stale suspension", () => first.IsAlive && first.Alpha == 1);
    }

    [Test]
    public void SourceSurvivesDelayedNestedReveal()
    {
        AddStep("push a page waiting for nested content", () =>
            stack.Push(second = new TransitionScreen(Color4.Coral, holdReveal: true)));
        AddUntilStep("destination loaded but not revealed", () => second.IsLoaded);
        AddWaitStep("wait past normal transition duration", 6);
        AddAssert("source still covers background", () => first.IsAlive && first.Alpha == 1);
        AddStep("nested content ready", () => second.FadeIn(100));
        AddUntilStep("source retires after reveal", () => second.Alpha == 1 && !first.IsAlive);
    }

    private partial class TransitionScreen : YokkoScreen
    {
        private readonly bool holdReveal;

        public TransitionScreen(Color4 colour, bool holdReveal = false)
        {
            this.holdReveal = holdReveal;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = colour };
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            if (!holdReveal)
                return;
            ClearTransforms(targetMember: nameof(Alpha));
            Alpha = 0.001f;
        }
    }

    private partial class DeferredScreenStack : ScreenStack
    {
        internal bool DeferLoad;
        private Action pending;

        public DeferredScreenStack() : base(suspendImmediately: false) { }

        protected override void LoadScreen(CompositeDrawable loader, Drawable toLoad, Action continuation)
        {
            if (DeferLoad)
                pending = () => base.LoadScreen(loader, toLoad, continuation);
            else
                base.LoadScreen(loader, toLoad, continuation);
        }

        internal void ReleaseLoad()
        {
            DeferLoad = false;
            Action load = pending;
            pending = null;
            load?.Invoke();
        }
    }
}
