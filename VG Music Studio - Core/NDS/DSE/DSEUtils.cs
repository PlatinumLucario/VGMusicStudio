﻿using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Core.Util;
using System;
using System.Diagnostics;

namespace Kermalis.VGMusicStudio.Core.NDS.DSE;

internal static class DSEUtils
{
	public static ReadOnlySpan<short> Duration16 => new short[128]
	{
		0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006, 0x0007,
		0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E, 0x000F,
		0x0010, 0x0011, 0x0012, 0x0013, 0x0014, 0x0015, 0x0016, 0x0017,
		0x0018, 0x0019, 0x001A, 0x001B, 0x001C, 0x001D, 0x001E, 0x001F,
		0x0020, 0x0023, 0x0028, 0x002D, 0x0033, 0x0039, 0x0040, 0x0048,
		0x0050, 0x0058, 0x0062, 0x006D, 0x0078, 0x0083, 0x0090, 0x009E,
		0x00AC, 0x00BC, 0x00CC, 0x00DE, 0x00F0, 0x0104, 0x0119, 0x012F,
		0x0147, 0x0160, 0x017A, 0x0196, 0x01B3, 0x01D2, 0x01F2, 0x0214,
		0x0238, 0x025E, 0x0285, 0x02AE, 0x02D9, 0x0307, 0x0336, 0x0367,
		0x039B, 0x03D1, 0x0406, 0x0442, 0x047E, 0x04C4, 0x0500, 0x0546,
		0x058C, 0x0622, 0x0672, 0x06CC, 0x071C, 0x0776, 0x07DA, 0x0834,
		0x0898, 0x0906, 0x096A, 0x09D8, 0x0A50, 0x0ABE, 0x0B40, 0x0BB8,
		0x0C3A, 0x0CBC, 0x0D48, 0x0DDE, 0x0E6A, 0x0F00, 0x0FA0, 0x1040,
		0x10EA, 0x1194, 0x123E, 0x12F2, 0x13B0, 0x146E, 0x1536, 0x15FE,
		0x16D0, 0x17A2, 0x187E, 0x195A, 0x1A40, 0x1B30, 0x1C20, 0x1D1A,
		0x1E1E, 0x1F22, 0x2030, 0x2148, 0x2260, 0x2382, 0x2710, 0x7FFF,
	};
	public static ReadOnlySpan<int> Duration32 => new int[128]
	{
		0x00000000, 0x00000004, 0x00000007, 0x0000000A, 0x0000000F, 0x00000015, 0x0000001C, 0x00000024,
		0x0000002E, 0x0000003A, 0x00000048, 0x00000057, 0x00000068, 0x0000007B, 0x00000091, 0x000000A8,
		0x00000185, 0x000001BE, 0x000001FC, 0x0000023F, 0x00000288, 0x000002D6, 0x0000032A, 0x00000385,
		0x000003E5, 0x0000044C, 0x000004BA, 0x0000052E, 0x000005A9, 0x0000062C, 0x000006B5, 0x00000746,
		0x00000BCF, 0x00000CC0, 0x00000DBD, 0x00000EC6, 0x00000FDC, 0x000010FF, 0x0000122F, 0x0000136C,
		0x000014B6, 0x0000160F, 0x00001775, 0x000018EA, 0x00001A6D, 0x00001BFF, 0x00001DA0, 0x00001F51,
		0x00002C16, 0x00002E80, 0x00003100, 0x00003395, 0x00003641, 0x00003902, 0x00003BDB, 0x00003ECA,
		0x000041D0, 0x000044EE, 0x00004824, 0x00004B73, 0x00004ED9, 0x00005259, 0x000055F2, 0x000059A4,
		0x000074CC, 0x000079AB, 0x00007EAC, 0x000083CE, 0x00008911, 0x00008E77, 0x000093FF, 0x000099AA,
		0x00009F78, 0x0000A56A, 0x0000AB80, 0x0000B1BB, 0x0000B81A, 0x0000BE9E, 0x0000C547, 0x0000CC17,
		0x0000FD42, 0x000105CB, 0x00010E82, 0x00011768, 0x0001207E, 0x000129C4, 0x0001333B, 0x00013CE2,
		0x000146BB, 0x000150C5, 0x00015B02, 0x00016572, 0x00017015, 0x00017AEB, 0x000185F5, 0x00019133,
		0x0001E16D, 0x0001EF07, 0x0001FCE0, 0x00020AF7, 0x0002194F, 0x000227E6, 0x000236BE, 0x000245D7,
		0x00025532, 0x000264CF, 0x000274AE, 0x000284D0, 0x00029536, 0x0002A5E0, 0x0002B6CE, 0x0002C802,
		0x000341B0, 0x000355F8, 0x00036A90, 0x00037F79, 0x000394B4, 0x0003AA41, 0x0003C021, 0x0003D654,
		0x0003ECDA, 0x000403B5, 0x00041AE5, 0x0004326A, 0x00044A45, 0x00046277, 0x00047B00, 0x7FFFFFFF,
	};
	private static ReadOnlySpan<ushort> PitchTable => new ushort[768]
	{
		0, 59, 118, 178, 237, 296, 356, 415,
		475, 535, 594, 654, 714, 773, 833, 893,
		953, 1013, 1073, 1134, 1194, 1254, 1314, 1375,
		1435, 1496, 1556, 1617, 1677, 1738, 1799, 1859,
		1920, 1981, 2042, 2103, 2164, 2225, 2287, 2348,
		2409, 2471, 2532, 2593, 2655, 2716, 2778, 2840,
		2902, 2963, 3025, 3087, 3149, 3211, 3273, 3335,
		3397, 3460, 3522, 3584, 3647, 3709, 3772, 3834,
		3897, 3960, 4022, 4085, 4148, 4211, 4274, 4337,
		4400, 4463, 4526, 4590, 4653, 4716, 4780, 4843,
		4907, 4971, 5034, 5098, 5162, 5226, 5289, 5353,
		5417, 5481, 5546, 5610, 5674, 5738, 5803, 5867,
		5932, 5996, 6061, 6125, 6190, 6255, 6320, 6384,
		6449, 6514, 6579, 6645, 6710, 6775, 6840, 6906,
		6971, 7037, 7102, 7168, 7233, 7299, 7365, 7431,
		7496, 7562, 7628, 7694, 7761, 7827, 7893, 7959,
		8026, 8092, 8159, 8225, 8292, 8358, 8425, 8492,
		8559, 8626, 8693, 8760, 8827, 8894, 8961, 9028,
		9096, 9163, 9230, 9298, 9366, 9433, 9501, 9569,
		9636, 9704, 9772, 9840, 9908, 9976, 10045, 10113,
		10181, 10250, 10318, 10386, 10455, 10524, 10592, 10661,
		10730, 10799, 10868, 10937, 11006, 11075, 11144, 11213,
		11283, 11352, 11421, 11491, 11560, 11630, 11700, 11769,
		11839, 11909, 11979, 12049, 12119, 12189, 12259, 12330,
		12400, 12470, 12541, 12611, 12682, 12752, 12823, 12894,
		12965, 13036, 13106, 13177, 13249, 13320, 13391, 13462,
		13533, 13605, 13676, 13748, 13819, 13891, 13963, 14035,
		14106, 14178, 14250, 14322, 14394, 14467, 14539, 14611,
		14684, 14756, 14829, 14901, 14974, 15046, 15119, 15192,
		15265, 15338, 15411, 15484, 15557, 15630, 15704, 15777,
		15850, 15924, 15997, 16071, 16145, 16218, 16292, 16366,
		16440, 16514, 16588, 16662, 16737, 16811, 16885, 16960,
		17034, 17109, 17183, 17258, 17333, 17408, 17483, 17557,
		17633, 17708, 17783, 17858, 17933, 18009, 18084, 18160,
		18235, 18311, 18387, 18462, 18538, 18614, 18690, 18766,
		18842, 18918, 18995, 19071, 19147, 19224, 19300, 19377,
		19454, 19530, 19607, 19684, 19761, 19838, 19915, 19992,
		20070, 20147, 20224, 20302, 20379, 20457, 20534, 20612,
		20690, 20768, 20846, 20924, 21002, 21080, 21158, 21236,
		21315, 21393, 21472, 21550, 21629, 21708, 21786, 21865,
		21944, 22023, 22102, 22181, 22260, 22340, 22419, 22498,
		22578, 22658, 22737, 22817, 22897, 22977, 23056, 23136,
		23216, 23297, 23377, 23457, 23537, 23618, 23698, 23779,
		23860, 23940, 24021, 24102, 24183, 24264, 24345, 24426,
		24507, 24589, 24670, 24752, 24833, 24915, 24996, 25078,
		25160, 25242, 25324, 25406, 25488, 25570, 25652, 25735,
		25817, 25900, 25982, 26065, 26148, 26230, 26313, 26396,
		26479, 26562, 26645, 26729, 26812, 26895, 26979, 27062,
		27146, 27230, 27313, 27397, 27481, 27565, 27649, 27733,
		27818, 27902, 27986, 28071, 28155, 28240, 28324, 28409,
		28494, 28579, 28664, 28749, 28834, 28919, 29005, 29090,
		29175, 29261, 29346, 29432, 29518, 29604, 29690, 29776,
		29862, 29948, 30034, 30120, 30207, 30293, 30380, 30466,
		30553, 30640, 30727, 30814, 30900, 30988, 31075, 31162,
		31249, 31337, 31424, 31512, 31599, 31687, 31775, 31863,
		31951, 32039, 32127, 32215, 32303, 32392, 32480, 32568,
		32657, 32746, 32834, 32923, 33012, 33101, 33190, 33279,
		33369, 33458, 33547, 33637, 33726, 33816, 33906, 33995,
		34085, 34175, 34265, 34355, 34446, 34536, 34626, 34717,
		34807, 34898, 34988, 35079, 35170, 35261, 35352, 35443,
		35534, 35626, 35717, 35808, 35900, 35991, 36083, 36175,
		36267, 36359, 36451, 36543, 36635, 36727, 36820, 36912,
		37004, 37097, 37190, 37282, 37375, 37468, 37561, 37654,
		37747, 37841, 37934, 38028, 38121, 38215, 38308, 38402,
		38496, 38590, 38684, 38778, 38872, 38966, 39061, 39155,
		39250, 39344, 39439, 39534, 39629, 39724, 39819, 39914,
		40009, 40104, 40200, 40295, 40391, 40486, 40582, 40678,
		40774, 40870, 40966, 41062, 41158, 41255, 41351, 41448,
		41544, 41641, 41738, 41835, 41932, 42029, 42126, 42223,
		42320, 42418, 42515, 42613, 42710, 42808, 42906, 43004,
		43102, 43200, 43298, 43396, 43495, 43593, 43692, 43790,
		43889, 43988, 44087, 44186, 44285, 44384, 44483, 44583,
		44682, 44781, 44881, 44981, 45081, 45180, 45280, 45381,
		45481, 45581, 45681, 45782, 45882, 45983, 46083, 46184,
		46285, 46386, 46487, 46588, 46690, 46791, 46892, 46994,
		47095, 47197, 47299, 47401, 47503, 47605, 47707, 47809,
		47912, 48014, 48117, 48219, 48322, 48425, 48528, 48631,
		48734, 48837, 48940, 49044, 49147, 49251, 49354, 49458,
		49562, 49666, 49770, 49874, 49978, 50082, 50187, 50291,
		50396, 50500, 50605, 50710, 50815, 50920, 51025, 51131,
		51236, 51341, 51447, 51552, 51658, 51764, 51870, 51976,
		52082, 52188, 52295, 52401, 52507, 52614, 52721, 52827,
		52934, 53041, 53148, 53256, 53363, 53470, 53578, 53685,
		53793, 53901, 54008, 54116, 54224, 54333, 54441, 54549,
		54658, 54766, 54875, 54983, 55092, 55201, 55310, 55419,
		55529, 55638, 55747, 55857, 55966, 56076, 56186, 56296,
		56406, 56516, 56626, 56736, 56847, 56957, 57068, 57179,
		57289, 57400, 57511, 57622, 57734, 57845, 57956, 58068,
		58179, 58291, 58403, 58515, 58627, 58739, 58851, 58964,
		59076, 59189, 59301, 59414, 59527, 59640, 59753, 59866,
		59979, 60092, 60206, 60319, 60433, 60547, 60661, 60774,
		60889, 61003, 61117, 61231, 61346, 61460, 61575, 61690,
		61805, 61920, 62035, 62150, 62265, 62381, 62496, 62612,
		62727, 62843, 62959, 63075, 63191, 63308, 63424, 63540,
		63657, 63774, 63890, 64007, 64124, 64241, 64358, 64476,
		64593, 64711, 64828, 64946, 65064, 65182, 65300, 65418,
	};
	private static ReadOnlySpan<byte> VolumeTable => new byte[724]
	{
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1,
		1, 2, 2, 2, 2, 2, 2, 2,
		2, 2, 2, 2, 2, 2, 2, 2,
		2, 2, 2, 2, 2, 2, 2, 2,
		2, 2, 2, 2, 2, 2, 2, 2,
		2, 2, 2, 2, 2, 3, 3, 3,
		3, 3, 3, 3, 3, 3, 3, 3,
		3, 3, 3, 3, 3, 3, 3, 3,
		3, 3, 3, 3, 3, 3, 4, 4,
		4, 4, 4, 4, 4, 4, 4, 4,
		4, 4, 4, 4, 4, 4, 4, 4,
		4, 5, 5, 5, 5, 5, 5, 5,
		5, 5, 5, 5, 5, 5, 5, 5,
		5, 6, 6, 6, 6, 6, 6, 6,
		6, 6, 6, 6, 6, 6, 6, 7,
		7, 7, 7, 7, 7, 7, 7, 7,
		7, 7, 7, 8, 8, 8, 8, 8,
		8, 8, 8, 8, 9, 9, 9, 9,
		9, 9, 9, 9, 9, 10, 10, 10,
		10, 10, 10, 10, 10, 11, 11, 11,
		11, 11, 11, 11, 11, 12, 12, 12,
		12, 12, 12, 12, 13, 13, 13, 13,
		13, 13, 13, 14, 14, 14, 14, 14,
		14, 15, 15, 15, 15, 15, 16, 16,
		16, 16, 16, 16, 17, 17, 17, 17,
		17, 18, 18, 18, 18, 19, 19, 19,
		19, 19, 20, 20, 20, 20, 21, 21,
		21, 21, 22, 22, 22, 22, 23, 23,
		23, 23, 24, 24, 24, 25, 25, 25,
		25, 26, 26, 26, 27, 27, 27, 28,
		28, 28, 29, 29, 29, 30, 30, 30,
		31, 31, 31, 32, 32, 33, 33, 33,
		34, 34, 35, 35, 35, 36, 36, 37,
		37, 38, 38, 38, 39, 39, 40, 40,
		41, 41, 42, 42, 43, 43, 44, 44,
		45, 45, 46, 46, 47, 47, 48, 48,
		49, 50, 50, 51, 51, 52, 52, 53,
		54, 54, 55, 56, 56, 57, 58, 58,
		59, 60, 60, 61, 62, 62, 63, 64,
		65, 66, 66, 67, 68, 69, 70, 70,
		71, 72, 73, 74, 75, 75, 76, 77,
		78, 79, 80, 81, 82, 83, 84, 85,
		86, 87, 88, 89, 90, 91, 92, 93,
		94, 95, 96, 97, 98, 99, 101, 102,
		103, 104, 105, 106, 108, 109, 110, 111,
		113, 114, 115, 117, 118, 119, 121, 122,
		124, 125, 126, 127,
	};
	public static ReadOnlySpan<byte> FixedRests => new byte[0x10]
	{
		96, 72, 64, 48, 36, 32, 24, 18, 16, 12, 9, 8, 6, 4, 3, 2,
	};

	public static bool IsStateRemovable(EnvelopeState state)
	{
		return state is EnvelopeState.Two or >= EnvelopeState.Seven;
	}


	#region FindChunk
	internal static long FindChunk(EndianBinaryReader r, string chunk)
	{
		long pos = -1;
		long oldPosition = r.Stream.Position;
		r.Stream.Position = 0;
		while (r.Stream.Position < r.Stream.Length)
		{
			string str = r.ReadString_Count(4);
			if (str == chunk)
			{
				pos = r.Stream.Position - 4;
				break;
			}
			switch (str)
			{
				case "swdb" or "swdl":
					{
						r.Stream.Position += 0x4C;
						break;
					}
				case "smdb" or "smdl":
					{
						r.Stream.Position += 0x3C;
						break;
					}
				default:
					{
						Debug.WriteLine($"Ignoring {str} chunk");
						r.Stream.Position += 0x8;
						uint length = r.ReadUInt32();
						r.Stream.Position += length;
						r.Stream.Align(16);
						break;
					}
			}
		}
		r.Stream.Position = oldPosition;
		return pos;
	}
	#endregion

	public static ushort GetChannelTimer(ushort baseTimer, int pitch)
	{
		int shift = 0;
		pitch = -pitch;

		while (pitch < 0)
		{
			shift--;
			pitch += 0x300;
		}

		while (pitch >= 0x300)
		{
			shift++;
			pitch -= 0x300;
		}

		ulong timer = (PitchTable[pitch] + 0x10000uL) * baseTimer;
		shift -= 16;
		if (shift <= 0)
		{
			timer >>= -shift;
		}
		else if (shift < 32)
		{
			if ((timer & (ulong.MaxValue << (32 - shift))) != 0)
			{
				return ushort.MaxValue;
			}
			timer <<= shift;
		}
		else
		{
			return ushort.MaxValue;
		}

		if (timer < 0x10)
		{
			return 0x10;
		}
		if (timer > ushort.MaxValue)
		{
			timer = ushort.MaxValue;
		}
		return (ushort)timer;
	}
	public static byte GetChannelVolume(int vol)
	{
		int a = vol / 0x80;
		if (a < -723)
		{
			a = -723;
		}
		else if (a > 0)
		{
			a = 0;
		}
		return VolumeTable[a + 723];
	}

}
