using NUnit.Framework;
using Yokko.Game.Screens.Editor;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class EditorPreviewClockTest
    {
        [Test]
        public void PlayUpdateAndStopAdvancePreviewTime()
        {
            var clock = new EditorPreviewClock();

            clock.Play(1000);
            Assert.That(clock.IsPlaying, Is.True);

            bool changed = clock.Update(250, 1000);

            Assert.That(changed, Is.True);
            Assert.That(clock.CurrentTimeMilliseconds, Is.EqualTo(250));

            clock.Stop();

            Assert.That(clock.IsPlaying, Is.False);
            Assert.That(clock.CurrentTimeMilliseconds, Is.EqualTo(0));
        }

        [Test]
        public void SeekClampsToDuration()
        {
            var clock = new EditorPreviewClock();

            clock.Seek(1500, 1000);
            Assert.That(clock.CurrentTimeMilliseconds, Is.EqualTo(1000));

            clock.Seek(-250, 1000);
            Assert.That(clock.CurrentTimeMilliseconds, Is.EqualTo(0));
        }

        [Test]
        public void UpdateStopsAtDuration()
        {
            var clock = new EditorPreviewClock();

            clock.Seek(900, 1000);
            clock.Play(1000);
            clock.Update(250, 1000);

            Assert.That(clock.CurrentTimeMilliseconds, Is.EqualTo(1000));
            Assert.That(clock.IsPlaying, Is.False);
        }
    }
}
