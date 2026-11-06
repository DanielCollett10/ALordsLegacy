using LordsLegacy.Game;
using LordsLegacy.Sim.Calendar;
using NUnit.Framework;

namespace LordsLegacy.Tests.EditMode
{
    public class SunLightModelTests
    {
        private static readonly Daylight Day = new Daylight(8, 16);

        private static GameTime At(int month, int day, int hour, int minute = 0)
        {
            int[] days = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            int doy = day;
            for (int i = 0; i < month - 1; i++) doy += days[i];
            return new GameTime(1, month, day, doy, Weekday.Monday, hour, minute, Season.Summer);
        }

        private static float Elev(GameTime t, double f = 0) =>
            SunLightModel.Evaluate(t, f, Day, false).ElevationDeg;

        [Test] public void DecSixAmIsBelowHorizon() => Assert.Less(Elev(At(12, 21, 6)), 0f);
        [Test] public void JunSixAmIsAboveHorizon() => Assert.Greater(Elev(At(6, 21, 6)), 0f);
        [Test] public void JunNoonHigherThanDecNoon() => Assert.Greater(Elev(At(6, 21, 12)), Elev(At(12, 21, 12)));

        [Test]
        public void SteadyLightIgnoresTime()
        {
            var a = SunLightModel.Evaluate(At(12, 21, 3), 0, Day, true);
            var b = SunLightModel.Evaluate(At(6, 21, 13, 37), 0.4, Day, true);
            Assert.AreEqual(a.ElevationDeg, b.ElevationDeg);
            Assert.AreEqual(a.AzimuthDeg, b.AzimuthDeg);
            Assert.AreEqual(a.Color, b.Color);
            Assert.AreEqual(a.Intensity, b.Intensity);
            Assert.AreEqual(a.AmbientIntensity, b.AmbientIntensity);
        }

        [Test]
        public void SmoothBetweenTicks()
        {
            float e0 = Elev(At(6, 21, 8, 0), 0);
            float eHalf = Elev(At(6, 21, 8, 0), 0.5);
            float e1 = Elev(At(6, 21, 8, 1), 0);
            Assert.Greater(eHalf, e0);
            Assert.Less(eHalf, e1);
        }

        [Test]
        public void NightIsDarkButReadable()
        {
            var s = SunLightModel.Evaluate(At(12, 21, 0), 0, Day, false);
            Assert.Greater(s.Intensity, 0f);
            Assert.Less(s.Intensity, 0.3f);
            Assert.Greater(s.AmbientIntensity, 0.1f);
        }
    }
}
