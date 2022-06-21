using System;
using System.Collections.Generic;
using System.Text;

namespace Octopus.Player.Core.Maths.Color
{
	public enum Illuminant
	{
		Unknown = 0,
		Daylight = 1,
		Fluorescent = 2,
		Tungsten = 3,
		Flash = 4,
		FineWeather = 9,
		CloudyWeather = 10,
		Shade = 11,
		DaylightFluorescent = 12,
		DayWhiteFluorescent = 13,
		CoolWhiteFluorescent = 14,
		WhiteFluorescent = 15,
		WarmWhiteFluorescent = 16,
		StandardLightA = 17,
		StandardLightB = 18,
		StandardLightC = 19,
		D55 = 20,
		D65 = 21,
		D75 = 22,
		D50 = 23,
		ISOStudioTungsten = 24,
		Other = 255
	};

	public static partial class Extensions
	{
		// Should match DNG SDK: source/dng_camera_profile.cpp
		public static float ColorTemperature(this Illuminant illuminant)
		{
			switch (illuminant)
			{
				case Illuminant.StandardLightA:
				case Illuminant.Tungsten:
					return 2850.0f;
				case Illuminant.ISOStudioTungsten:
					return 3200.0f;
				case Illuminant.D50:
					return 5000.0f;
				case Illuminant.D55:
				case Illuminant.Daylight:
				case Illuminant.FineWeather:
				case Illuminant.Flash:
				case Illuminant.StandardLightB:
					return 5500.0f;
				case Illuminant.D65:
				case Illuminant.StandardLightC:
				case Illuminant.CloudyWeather:
					return 6500.0f;
				case Illuminant.D75:
				case Illuminant.Shade:
					return 7500.0f;
				case Illuminant.DaylightFluorescent:
					return (5700.0f + 7100.0f) * 0.5f;
				case Illuminant.DayWhiteFluorescent:
					return (4600.0f + 5500.0f) * 0.5f;
				case Illuminant.CoolWhiteFluorescent:
				case Illuminant.Fluorescent:
					return (3800.0f + 4500.0f) * 0.5f;
				case Illuminant.WhiteFluorescent:
					return (3250.0f + 3800.0f) * 0.5f;
				case Illuminant.WarmWhiteFluorescent:
					return (2600.0f + 3250.0f) * 0.5f;
				default:
					throw new Exception("Unknown illuminant");
			}
		}
	}
}
