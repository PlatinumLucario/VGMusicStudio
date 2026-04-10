using System;
using System.Buffers.Binary;
using System.Linq;
using Kermalis.MIDI;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K;

internal sealed partial class MP2KLoadedSong
{
	private void TryPlayNote(MP2KTrack track, byte note, byte velocity, byte addedDuration)
	{
		int n = note + track.Transpose;
		if (n < 0)
		{
			n = 0;
		}
		else if (n > 0x7F)
		{
			n = 0x7F;
		}
		note = (byte)n;
		track.PrevNote = note;
		track.PrevVelocity = velocity;
		// Tracks do not play unless they have had a voice change event
		if (track.Ready)
		{
			PlayNote(_player.Config.ROM, track, note, velocity, addedDuration);

			track.UpdateVolume = true;
			track.UpdatePitch = true;
		}
	}
	// private void PlayNote(byte[] rom, MP2KTrack track, byte cmd)
	private void PlayNote(byte[] rom, MP2KTrack track, byte note, byte velocity, byte addedDuration)
	{
		// foreach ((byte command, byte len) in MP2KUtils.NoteLUT)
		// {
		// 	if (cmd == command)
		// 	{
		// 		track.PrevLength = len;
		// 		break;
		// 	}
		// }

		// if (rom[track.Position] < 0x80)
		// {
		// 	track.PrevNote = rom[track.Position++];

		// 	if (rom[track.Position] < 0x80)
		// 	{
		// 		track.PrevVelocity = rom[track.Position++];

		// 		if (rom[track.Position] < 0x80)
		// 		{
		// 			track.PrevLength += rom[track.Position++];
		// 		}
		// 	}
		// }
		bool fromDrum = false;
		int offset = _soundBankOffset + (track.Voice * 12);
		byte rootNote;
		sbyte rythmPan = 0;
		while (true)
		{
			var v = new VoiceEntry(rom.AsSpan(offset));
			if (v.Type == (int)VoiceFlags.KeySplit)
			{
				fromDrum = false; // In case there is a multi within a drum
				byte inst = rom[v.Int8 - GBAUtils.CARTRIDGE_OFFSET + note];
				offset = v.Int4 - GBAUtils.CARTRIDGE_OFFSET + (inst * 12);
				rootNote = track.PrevNote;
			}
			else if (v.Type == (int)VoiceFlags.Drum)
			{
				fromDrum = true;
				offset = v.Int4 - GBAUtils.CARTRIDGE_OFFSET + (note * 12);
				if ((v.PanSweep & 0x80) != 0)
				{
					rythmPan = (sbyte)((v.PanSweep - 0xC0) * 2);
				}
				rootNote = v.RootNote;
			}
			else
			{
				rootNote = v.RootNote;
				track.LFODelayCount = track.LFODelay;
				if (track.LFODelay != 0)
				{
					track.ResetLFOValue();
				}

				var ni = new NoteInfo
				{
					Duration = track.RunCmd == 0xCF ? -1 : (MP2KUtils.RestTable[track.RunCmd - 0xCF] + addedDuration),
					// Duration = track.PrevLength,
					Note = fromDrum ? v.RootNote : note,
					OriginalNote = note,
					Velocity = velocity,
					Priority = track.Priority,
					RhythmPan = rythmPan,
					PseudoEchoVolume = track.PseudoEchoVolume,
					PseudoEchoLength = track.PseudoEchoLength,
					TrackIndex = track.Index,
					PSGLength = v.TimeLength,
				};
				// static int OrderChannels(MP2KChannel c)
				// {
				// 	return c.Track is null ? 0xFF : c.Track.Index;
				// }
				// int instPan = (v.PanSweep & 0x80) != 0 ? v.PanSweep - 0xC0 : 0;
				var type = (VoiceType)(v.Type & 0x7);
				switch (type)
				{
					case VoiceType.PCM8:
						{
							SampleInfo sInfo = new(rom, v.Int4 - GBAUtils.CARTRIDGE_OFFSET);

							bool bFixed = (v.Type & (int)VoiceFlags.Fixed) != 0;
							// bool bCompressed = _player.Config.HasPokemonCompression && ((v.Type & (int)VoiceFlags.Compressed) != 0);
							// _player.MMixer.AllocPCM8Channel(track, v.ADSR, ni,
							// 		track.GetVolume(), track.GetPanpot(), v.PanSweep, track.GetPitch(),
							// 		bFixed, bCompressed, v.Int4 - GBAUtils.CARTRIDGE_OFFSET);

							MP2KPCM8Channel nChn = new(_player.MContext, _player.MMixer, track, sInfo, v.ADSR, ni, bFixed);

							if (_player.MContext.PCM8Channels.Count <= track.Index)
							{
								_player.MContext.PCM8Channels.Add(nChn);
							}
							else if (_player.MContext.PCM8Channels[track.Index].Track.Index != track.Index || _player.MContext.PCM8Channels[track.Index] is null)
							{
								_player.MContext.PCM8Channels.Insert(track.Index, nChn);
							}
							else
							{
								_player.MContext.PCM8Channels[track.Index] = nChn;
							}
							return;
						}
					case VoiceType.Square1:
						{
							MP2KSquareChannel nChn = new(_player.MContext, _player.MMixer, track, v.Int4, v.ADSR, ni, v.PanSweep);
							if (nChn.State < EnvelopeState.Releasing && nChn.Track!.Index < track.Index)
							{
								return;
							}
							if (_player.MContext.Square1Channels.Count <= track.Index)
							{
								_player.MContext.Square1Channels.Add(nChn);
							}
							else if (_player.MContext.Square1Channels[track.Index].Track.Index != track.Index || _player.MContext.Square1Channels[track.Index] is null)
							{
								_player.MContext.Square1Channels.Insert(track.Index, nChn);
							}
							else
							{
								_player.MContext.Square1Channels[track.Index] = nChn;
							}
							return;
						}
					case VoiceType.Square2:
						{
							// _player.MMixer.AllocPSGChannel(track, v.ADSR, ni,
							// 		track.GetVolume(), track.GetPanpot(), v.PanSweep, track.GetPitch(),
							// 		type, (SquarePattern)v.Int4);
							MP2KSquareChannel nChn = new(_player.MContext, _player.MMixer, track, v.Int4, v.ADSR, ni, 0);
							if (nChn.State < EnvelopeState.Releasing && nChn.Track!.Index < track.Index)
							{
								return;
							}
							if (_player.MContext.Square2Channels.Count <= track.Index)
							{
								_player.MContext.Square2Channels.Add(nChn);
							}
							else if (_player.MContext.Square2Channels[track.Index].Track.Index != track.Index || _player.MContext.Square2Channels[track.Index] is null)
							{
								_player.MContext.Square2Channels.Insert(track.Index, nChn);
							}
							else
							{
								_player.MContext.Square2Channels[track.Index] = nChn;
							}
							return;
						}
					case VoiceType.PCM4:
						{
							// _player.MMixer.AllocPSGChannel(track, v.ADSR, ni,
							// 		track.GetVolume(), track.GetPanpot(), v.PanSweep, track.GetPitch(),
							// 		type, v.Int4 - GBAUtils.CARTRIDGE_OFFSET);
							MP2KPCM4Channel nChn = new(_player.MContext, _player.MMixer, track, v.Int4, v.ADSR, ni, _player.MContext.PlayerSoundMode.AccurateCh3Volume);
							if (nChn.State < EnvelopeState.Releasing && nChn.Track!.Index < track.Index)
							{
								return;
							}
							if (_player.MContext.PCM4Channels.Count <= track.Index)
							{
								_player.MContext.PCM4Channels.Add(nChn);
							}
							else if (_player.MContext.PCM4Channels[track.Index].Track.Index != track.Index || _player.MContext.PCM4Channels[track.Index] is null)
							{
								_player.MContext.PCM4Channels.Insert(track.Index, nChn);
							}
							else
							{
								_player.MContext.PCM4Channels[track.Index] = nChn;
							}
							return;
						}
					case VoiceType.Noise:
						{
							// _player.MMixer.AllocPSGChannel(track, v.ADSR, ni,
							// 		track.GetVolume(), track.GetPanpot(), v.PanSweep, track.GetPitch(),
							// 		type, (NoisePattern)v.Int4);
							MP2KNoiseChannel nChn = new(_player.MContext, _player.MMixer, track, v.Int4, v.ADSR, ni);
							if (nChn.State < EnvelopeState.Releasing && nChn.Track!.Index < track.Index)
							{
								return;
							}
							if (_player.MContext.NoiseChannels.Count <= track.Index)
							{
								_player.MContext.NoiseChannels.Add(nChn);
							}
							else if (_player.MContext.NoiseChannels[track.Index].Track.Index != track.Index || _player.MContext.NoiseChannels[track.Index] is null)
							{
								_player.MContext.NoiseChannels.Insert(track.Index, nChn);
							}
							else
							{
								_player.MContext.NoiseChannels[track.Index] = nChn;
							}
							return;
						}
				}
				return; // Prevent infinite loop with invalid instruments
			}
		}
	}
	public void ExecuteNext(MP2KTrack track, ref bool update)
	{
		byte[] rom = _player.Config.ROM;
		byte cmd = rom[track.Position++];
		// byte cmd = rom[track.Position];
		if (cmd >= 0xBD) // Commands that work within running status
		{
			track.RunCmd = cmd;
		}
		// if (cmd < 0x80)
		// {
		// 	cmd = track.RunCmd;
		// 	if (cmd < 0x80)
		// 	{
		// 		FineCommand(track);
		// 		return;
		// 	}
		// }
		// else
		// {
		// 	track.Position++;
		// 	if (cmd >= 0xBD) // Commands that work within running status
		// 	{
		// 		track.RunCmd = cmd;
		// 	}
		// }

		if (track.RunCmd >= 0xCF && cmd <= 0x7F) // Within running status
		{
			byte peek0 = rom[track.Position];
			byte peek1 = rom[track.Position + 1];
			byte velocity, addedDuration;
			if (peek0 > 0x7F)
			{
				velocity = track.PrevVelocity;
				addedDuration = 0;
			}
			else if (peek1 > 3)
			{
				track.Position++;
				velocity = peek0;
				addedDuration = 0;
			}
			else
			{
				track.Position += 2;
				velocity = peek0;
				addedDuration = peek1;
			}
			TryPlayNote(track, cmd, velocity, addedDuration);
		}
		else if (cmd >= 0xCF)
		{
			byte peek0 = rom[track.Position];
			byte peek1 = rom[track.Position + 1];
			byte peek2 = rom[track.Position + 2];
			byte key, velocity, addedDuration;
			if (peek0 > 0x7F)
			{
				key = track.PrevNote;
				velocity = track.PrevVelocity;
				addedDuration = 0;
			}
			else if (peek1 > 0x7F)
			{
				track.Position++;
				key = peek0;
				velocity = track.PrevVelocity;
				addedDuration = 0;
			}
			else if (cmd == 0xCF || peek2 > 3)
			{
				track.Position += 2;
				key = peek0;
				velocity = peek1;
				addedDuration = 0;
			}
			else
			{
				track.Position += 3;
				key = peek0;
				velocity = peek1;
				addedDuration = peek2;
			}
			TryPlayNote(track, key, velocity, addedDuration);
			// PlayNote(rom, track, cmd);
		}
		else if (cmd >= 0x80 && cmd <= 0xB0)
		{
			track.Rest = MP2KUtils.RestTable[cmd - 0x80];
			// foreach ((byte command, byte len) in MP2KUtils.DelayLUT)
			// {
			// 	if (cmd == command)
			// 	{
			// 		track.Rest = len;
			// 	}
			// }
		}
		else if (track.RunCmd < 0xCF && cmd <= 0x7F)
		{
			ExecuteCommand(rom, track, cmd, ref update);
		}
		else if (cmd > 0xB0 && cmd < 0xCF)
		{
			ExecuteCommand(rom, track, cmd, ref update);
		}

		if (!track.Ready)
		{
			return;
		}

		track.Pitch = track.GetPitch();

		if (!track.UpdateVolume && !track.UpdatePitch)
		{
			return;
		}

		void SetVolPitch(MP2KChannel chn)
		{
			if (chn is not null)
			{
				if (chn.Track == track)
				{
					if (track.UpdateVolume)
					{
						chn.SetVolume(track.GetVolume(), track.GetPanpot());
					}
					if (track.UpdatePitch)
					{
						chn.SetPitch(track.Pitch);
					}
				}
			}
		}

		foreach (MP2KPCM8Channel chn in _player.MContext.PCM8Channels)
		{
			SetVolPitch(chn);
		}
		foreach (MP2KSquareChannel chn in _player.MContext.Square1Channels)
		{
			SetVolPitch(chn);
		}
		foreach (MP2KSquareChannel chn in _player.MContext.Square2Channels)
		{
			SetVolPitch(chn);
		}
		foreach (MP2KPCM4Channel chn in _player.MContext.PCM4Channels)
		{
			SetVolPitch(chn);
		}
		foreach (MP2KNoiseChannel chn in _player.MContext.NoiseChannels)
		{
			SetVolPitch(chn);
		}

		track.UpdateVolume = false;
		track.UpdatePitch = false;
	}

	private void ExecuteCommand(byte[] rom, MP2KTrack track, byte cmd, ref bool update)
	{
		bool isRunning = track.RunCmd < 0xCF && cmd <= 0x7F;
		SequenceCommand cmdType = isRunning ? (SequenceCommand)track.RunCmd : (SequenceCommand)cmd;
		switch (cmdType)
		{
			case SequenceCommand.Fine:
				{
					FineCommand(track);
					break;
				}
			case SequenceCommand.Goto:
				{
					track.Position = GBAUtils.ReadOffsetData(rom.AsSpan(track.Position, 4));
					break;
				}
			case SequenceCommand.Pattern:
				{
					if (track.CallStackDepth >= track.CallStack.Length)
					{
						FineCommand(track);
						break;
					}
					track.CallStack[track.CallStackDepth++] = track.Position + 4;
					track.Position = GBAUtils.ReadOffsetData(rom.AsSpan(track.Position, 4));
					break;
				}
			case SequenceCommand.PatternEnd:
				{
					if (track.CallStackDepth is 0)
					{
						break;
					}

					track.Position = track.CallStack[--track.CallStackDepth];
					break;
				}
			case SequenceCommand.Repeat:
				{
					byte count = rom[track.Position++];
					if (count is 0)
					{
						FineCommand(track);
					}
					if (++track.RepeatTimes < count)
					{
						track.Position = GBAUtils.ReadOffsetData(rom.AsSpan(track.Position, 4));
					}
					else
					{
						track.RepeatTimes = 0;
						track.Position += 4;
					}
					break;
				}
			case SequenceCommand.MemoryAccess:
				{
					MemoryAccessCommand(track);
					break;
				}
			case SequenceCommand.Priority:
				{
					track.Priority = rom[track.Position++];
					break;
				}
			case SequenceCommand.Tempo:
				{
					_player.Tempo = (ushort)(rom[track.Position++] * 2);
					break;
				}
			case SequenceCommand.KeyShift:
				{
					track.Transpose = (sbyte)rom[track.Position++];
					break;
				}
			// Commands that work within running status:
			case SequenceCommand.Voice:
				{
					if (isRunning)
					{
						track.Voice = cmd;
					}
					else
					{
						track.Voice = rom[track.Position++];
						track.Ready = true; // To indicate that it's ready to be used in running status
					}
					break;
				}
			case SequenceCommand.Volume:
				{
					track.Volume = isRunning ? cmd : rom[track.Position++];
					track.UpdateVolume = true;
					update = true;
					break;
				}
			case SequenceCommand.Panpot:
				{
					track.Panpot = (sbyte)((isRunning ? cmd : rom[track.Position++]) - 0x40);
					track.UpdateVolume = true;
					update = true;
					break;
				}
			case SequenceCommand.Bend:
				{
					track.PitchBend = (sbyte)((isRunning ? cmd : rom[track.Position++]) - 0x40);
					track.UpdatePitch = true;
					update = true;
					break;
				}
			case SequenceCommand.BendRange:
				{
					track.PitchBendRange = isRunning ? cmd : rom[track.Position++];
					track.UpdatePitch = true;
					update = true;
					break;
				}
			case SequenceCommand.LFOSpeed:
				{
					track.LFOSpeed = isRunning ? cmd : rom[track.Position++];
					if (track.LFOSpeed is 0)
					{
						track.ResetLFOValue();
					}
					track.LFODelayCount = 0;
					update = true;
					break;
				}
			case SequenceCommand.LFODelay:
				{
					track.LFODelay = isRunning ? cmd : rom[track.Position++];
					track.LFOPhase = 0;
					track.LFODelayCount = 0;
					update = true;
					break;
				}
			case SequenceCommand.Modulation:
				{
					track.LFODepth = isRunning ? cmd : rom[track.Position++];
					update = true;
					if (track.LFODepth is 0)
					{
						track.ResetLFOValue();
					}
					break;
				}
			case SequenceCommand.ModulationType:
				{
					LFOType modt = (LFOType)(isRunning ? cmd : rom[track.Position++]);
					if (modt == track.LFOType)
					{
						return;
					}
					track.LFOType = modt;
					track.UpdateVolume = true;
					track.UpdatePitch = true;
					update = true;
					break;
				}
			case SequenceCommand.Tune:
				{
					track.Tune = (sbyte)((isRunning ? cmd : rom[track.Position++]) - 0x40);
					track.UpdatePitch = true;
					update = true;
					break;
				}
			case SequenceCommand.ExtendedCommand:
				{
					if (isRunning)
					{
						track.Position++; // Extended commands don't have a running status
					}
					else
					{
						ExtendedCommand(track);
					}
					break;
				}
			case SequenceCommand.EndOfTie:
				{
					byte key;
					if (isRunning)
					{
						track.PrevNote = cmd;
						key = cmd;
					}
					else
					{
						key = rom[track.Position];
						if (key >= 0x80)
						{
							key = track.PrevNote;
						}
						else
						{
							track.Position++;
							track.PrevNote = key;
						}
					}
					foreach (MP2KChannel channel in track.Channels)
					{
						if (channel.State == EnvelopeState.Dead)
						{
							continue;
						}
						if (channel.IsReleasing())
						{
							continue;
						}
						if (channel.Note.Note == key)
						{
							channel.Release();
							break;
						}
					}
					break;
				}
			default:
				{
					FineCommand(track);
					break;
				}
		}
	}

	internal static void FineCommand(MP2KTrack track)
	{
		for (int i = 0; i < track.Channels.Count; i++)
		{
			track.Channels[i].Release();
			track.Channels[i].RemoveFromTrack();
		}

		// if (track.Channels.Count > 0)
		// {
		// 	track.Channels.Clear();
		// }

		track.Stopped = true;
		// track.ActiveNotes.AsSpan().Clear();
		// track.ActiveVoiceTypes = VoiceConfigType.None;
	}

	internal void MemoryAccessCommand(MP2KTrack track)
	{
		Span<byte> rom = MP2KEngine.MP2KInstance!.Config.ROM;

		MemoryAccessType opCode = (MemoryAccessType)rom[track.Position++];
		ref byte memory = ref _player.MContext.MemAccArea.Span[rom[track.Position++]];
		byte data = rom[track.Position++];

		switch (opCode)
		{
			case MemoryAccessType.MemSet:
				{
					memory = data;
					return;
				}
			case MemoryAccessType.MemAdd:
				{
					memory += data;
					return;
				}
			case MemoryAccessType.MemSub:
				{
					memory -= data;
					return;
				}
			case MemoryAccessType.MemMemSet:
				{
					memory = _player.MContext.MemAccArea.Span[data];
					return;
				}
			case MemoryAccessType.MemMemAdd:
				{
					memory += _player.MContext.MemAccArea.Span[data];
					return;
				}
			case MemoryAccessType.MemMemSub:
				{
					memory -= _player.MContext.MemAccArea.Span[data];
					return;
				}
			case MemoryAccessType.MemBEq:
				{
					if (memory == data)
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemBNEq:
				{
					if (memory != data)
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemBHi:
				{
					if (memory > data)
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemBHS:
				{
					if (memory >= data)
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemBLS:
				{
					if (memory <= data)
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemBLo:
				{
					if (memory < data)
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemMemBEq:
				{
					if (memory == _player.MContext.MemAccArea.Span[data])
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemMemBNEq:
				{
					if (memory != _player.MContext.MemAccArea.Span[data])
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemMemBHi:
				{
					if (memory > _player.MContext.MemAccArea.Span[data])
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemMemBHS:
				{
					if (memory >= _player.MContext.MemAccArea.Span[data])
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemMemBLS:
				{
					if (memory <= _player.MContext.MemAccArea.Span[data])
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			case MemoryAccessType.MemMemBLo:
				{
					if (memory < _player.MContext.MemAccArea.Span[data])
					{
						track.Position = GBAUtils.ReadOffsetData(rom[track.Position..]);
						return;
					}
					break;
				}
			default:
				{
					return;
				}
		}

		track.Position += 4;
	}

	internal static void ExtendedCommand(MP2KTrack track)
	{
		Span<byte> rom = MP2KEngine.MP2KInstance!.Config.ROM;
		ExtendedCommandType xCmdType = (ExtendedCommandType)rom[track.Position++];

		switch (xCmdType)
		{
			case ExtendedCommandType.xWAVE:
				{
					track.Position += 4;
					break;
				}
			case ExtendedCommandType.xTYPE:
				{
					track.Position++;
					break;
				}
			case ExtendedCommandType.xATTA:
				{
					track.Position++;
					break;
				}
			case ExtendedCommandType.xDECA:
				{
					track.Position++;
					break;
				}
			case ExtendedCommandType.xSUST:
				{
					track.Position++;
					break;
				}
			case ExtendedCommandType.xRELA:
				{
					track.Position++;
					break;
				}
			case ExtendedCommandType.xIECV:
				{
					track.PseudoEchoVolume = rom[track.Position++];
					break;
				}
			case ExtendedCommandType.xIECL:
				{
					track.PseudoEchoLength = rom[track.Position++];
					break;
				}
			case ExtendedCommandType.xLENG:
				{
					track.Position++;
					break;
				}
			case ExtendedCommandType.xSWEE:
				{
					track.Position++;
					break;
				}
			case ExtendedCommandType.xWAIT:
				{
					track.Rest = (byte)BinaryPrimitives.ReadUInt16LittleEndian(rom[track.Position..]);
					track.Position += 2;
					break;
				}
			case ExtendedCommandType.xSOFF:
				{
					track.Position += 4;
					break;
				}
			default:
				{
					FineCommand(track);
					break;
				}
		}
	}

	public void UpdateInstrumentCache(byte voice, out string str)
	{
		byte t = _player.Config.ROM[_soundBankOffset + (voice * 12)];
		if (t == (byte)VoiceFlags.KeySplit)
		{
			str = "Key Split";
		}
		else if (t == (byte)VoiceFlags.Drum)
		{
			str = "Drum";
		}
		else
		{
			switch ((VoiceType)(t & 0x7)) // Disregard the other flags
			{
				case VoiceType.PCM8: str = "PCM8"; break;
				case VoiceType.Square1: str = "Square 1"; break;
				case VoiceType.Square2: str = "Square 2"; break;
				case VoiceType.PCM4: str = "PCM4"; break;
				case VoiceType.Noise: str = "Noise"; break;
				case VoiceType.Invalid5: str = "Invalid 5"; break;
				case VoiceType.Invalid6: str = "Invalid 6"; break;
				default: str = "Invalid 7"; break; // VoiceType.Invalid7
			}
		}
	}
	public void UpdateSongState(SongState info, string?[] voiceTypeCache)
	{
		for (int trackIndex = 0; trackIndex < Tracks.Length; trackIndex++)
		{
			Tracks[trackIndex].UpdateSongState(info.Tracks[trackIndex], this, voiceTypeCache);
		}
	}
}
