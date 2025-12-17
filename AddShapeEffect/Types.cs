using System.Reflection;

namespace AddShapeEffect
{
    internal static class Types
    {
        static Assembly YukkuriMovieMaker = Assembly.Load("YukkuriMovieMaker");

        static Assembly YukkuriMovieMakerPluginCommunity = Assembly.Load("YukkuriMovieMaker.Plugin.Community");

        public static Type? AudioSpectrumShapePlugin = YukkuriMovieMaker.GetType("YukkuriMovieMaker.Shape.AudioSpectrumShapePlugin");

        public static Type? LineShapePlugin = YukkuriMovieMaker.GetType("YukkuriMovieMaker.Shape.LineShapePlugin");

        public static Type? PenShapePlugin = YukkuriMovieMakerPluginCommunity.GetType("YukkuriMovieMaker.Plugin.Community.Shape.Pen.PenShapePlugin");

        public static Type? NumberText = YukkuriMovieMakerPluginCommunity.GetType("YukkuriMovieMaker.Plugin.Community.Shape.NumberText.NumberText");
    }
}   