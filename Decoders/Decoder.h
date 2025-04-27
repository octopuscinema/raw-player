#pragma once

namespace Octopus::Player::Decoders
{
	using Kelvin = float;
	using Tint = float;
	using WhiteBalance = std::pair<Kelvin, Tint>;

	static const WhiteBalance DefaultWhiteBalance = std::make_pair(5500.f, 10.f);

    struct VideoFrameMetadata
	{
		std::optional<maths::TimecodeHMSF> Timecode;
		std::optional<io::eCFAPattern> CFAPattern;
		std::optional<float> BaselineExposure;
	};
}