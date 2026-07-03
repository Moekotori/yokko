using osu.Framework.Testing;

namespace Yokko.Game.Tests.Visual
{
    public abstract partial class YokkoTestScene : TestScene
    {
        protected override ITestSceneTestRunner CreateRunner() => new YokkoTestSceneTestRunner();

        private partial class YokkoTestSceneTestRunner : YokkoGameBase, ITestSceneTestRunner
        {
            private TestSceneTestRunner.TestRunner runner;

            protected override void LoadAsyncComplete()
            {
                base.LoadAsyncComplete();
                Add(runner = new TestSceneTestRunner.TestRunner());
            }

            public void RunTestBlocking(TestScene test) => runner.RunTestBlocking(test);
        }
    }
}
