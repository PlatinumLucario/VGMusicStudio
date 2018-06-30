﻿using System;
using System.Collections.Generic;
using System.Linq;
using GBAMusicStudio.Core.M4A;
using static GBAMusicStudio.Core.M4A.M4AStructs;
using GBAMusicStudio.MIDI;
using GBAMusicStudio.Util;
using System.Threading;

namespace GBAMusicStudio.Core
{
    enum State
    {
        Playing,
        Paused,
        Stopped
    }

    internal static class SongPlayer
    {
        internal static readonly FMOD.System System;
        static readonly TimeBarrier time;
        static Thread thread;

        static readonly Instrument[] dsInstruments;
        static readonly Instrument[] gbInstruments;
        static readonly Instrument[] allInstruments;

        internal static Dictionary<uint, FMOD.Sound> Sounds { get; private set; }
        internal static readonly uint SQUARE12_ID = 0xFFFFFFFF,
            SQUARE25_ID = SQUARE12_ID - 1,
            SQUARE50_ID = SQUARE25_ID - 1,
            SQUARE75_ID = SQUARE50_ID - 1,
            NOISE0_ID = SQUARE75_ID - 1,
            NOISE1_ID = NOISE0_ID - 1;

        internal static byte Tempo;
        static int tempoStack;
        static uint position;
        static readonly Track[] tracks;

        internal static Song Song { get; private set; }
        internal static VoiceTable VoiceTable;
        internal static int NumTracks => Song == null ? 0 : Song.NumTracks.Clamp(0, 16);

        static SongPlayer()
        {
            FMOD.Factory.System_Create(out System);
            System.init(Config.DirectCount + 4, FMOD.INITFLAGS.NORMAL, (IntPtr)0);

            dsInstruments = new Instrument[Config.DirectCount];
            gbInstruments = new Instrument[4];
            for (int i = 0; i < dsInstruments.Length; i++)
                dsInstruments[i] = new Instrument();
            for (int i = 0; i < 4; i++)
                gbInstruments[i] = new Instrument();
            allInstruments = dsInstruments.Union(gbInstruments).ToArray();

            tracks = new Track[16];
            for (byte i = 0; i < 16; i++)
                tracks[i] = new Track(i);

            ClearVoices();

            time = new TimeBarrier();
        }

        internal static void ClearVoices()
        {
            if (Sounds != null)
            {
                foreach (var s in Sounds.Values)
                    s.release();
                Sounds.Clear();
                VoiceTable = null;
                SMulti.LoadedMultis.Clear();
                SDrum.LoadedDrums.Clear();
            }
            else
            {
                Sounds = new Dictionary<uint, FMOD.Sound>();
            }
            PSGSquare();
            PSGNoise();
        }
        static void PSGSquare()
        {
            byte[] simple = { 1, 2, 4, 6 };
            uint len = 0x100;
            var buf = new byte[len];

            for (uint i = 0; i < 4; i++) // Squares
            {
                for (int j = 0; j < len; j++)
                    buf[j] = (byte)(j < simple[i] * 0x20 ? 0xF * Config.PSGVolume : 0x0);
                var ex = new FMOD.CREATESOUNDEXINFO()
                {
                    defaultfrequency = 112640,
                    format = FMOD.SOUND_FORMAT.PCM8,
                    length = len,
                    numchannels = 1
                };
                System.createSound(buf, FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW | FMOD.MODE.LOOP_NORMAL | FMOD.MODE.LOWMEM, ref ex, out FMOD.Sound snd);
                Sounds.Add(SQUARE12_ID - i, snd);
            }
        }
        static void PSGNoise()
        {
            uint[] simple = { 32768, 256 };
            var rand = new Random();

            for (uint i = 0; i < 2; i++)
            {
                uint len = simple[i];
                var buf = new byte[len];

                for (int j = 0; j < len; j++)
                    buf[j] = (byte)rand.Next(0xF * Config.PSGVolume);

                var ex = new FMOD.CREATESOUNDEXINFO()
                {
                    defaultfrequency = 4096,
                    format = FMOD.SOUND_FORMAT.PCM8,
                    length = len,
                    numchannels = 1
                };
                System.createSound(buf, FMOD.MODE.OPENMEMORY | FMOD.MODE.OPENRAW | FMOD.MODE.LOOP_NORMAL | FMOD.MODE.LOWMEM, ref ex, out FMOD.Sound snd);
                Sounds.Add(NOISE0_ID - i, snd);
            }
        }

        internal static State State { get; private set; }
        internal delegate void SongEndedEvent();
        internal static event SongEndedEvent SongEnded;

        internal static void SetVolume(float v)
        {
            System.getMasterChannelGroup(out FMOD.ChannelGroup parentGroup);
            parentGroup.setVolume(v);
        }
        internal static void SetMute(int i, bool m) => tracks[i].Group.setMute(m);
        internal static void SetPosition(uint p)
        {
            bool pause = State == State.Playing;
            if (pause) Pause();
            position = p;
            for (int i = NumTracks - 1; i >= 0; i--)
            {
                var track = tracks[i];
                track.Init();
                int elapsed = 0;
                while (!track.Stopped)
                {
                    ExecuteNext(i);
                    // elapsed == 400, delay == 4, p == 402
                    if (elapsed <= p && elapsed + track.Delay > p)
                    {
                        track.Delay -= (sbyte)(p - elapsed);
                        foreach (var ins in track.Instruments)
                            ins.Stop();
                        break;
                    }
                    elapsed += track.Delay;
                    track.Delay = 0;
                }
            }
            if (pause) Pause();
        }

        internal static void LoadROMSong(ushort num, byte table)
        {
            Song = new ROMSong(num, table);

            MIDIKeyboard.Start();
        }
        internal static void LoadASMSong(Assembler assembler, string headerLabel)
        {
            Song = new ASMSong(assembler, headerLabel);
        }

        internal static void Play()
        {
            Stop();

            if (NumTracks == 0)
            {
                SongEnded?.Invoke();
                return;
            }

            for (int i = 0; i < NumTracks; i++)
                tracks[i].Init();

            position = 0; tempoStack = 0;
            Tempo = 150 / 2;

            StartThread();
        }
        internal static void Pause()
        {
            if (State == State.Paused)
            {
                StartThread();
            }
            else
            {
                StopThread();
                System.getMasterChannelGroup(out FMOD.ChannelGroup parentGroup);
                parentGroup.setMute(true);
                State = State.Paused;
            }
        }
        internal static void Stop()
        {
            StopThread();
            foreach (Instrument i in allInstruments)
                i.Stop();
        }
        static void StartThread()
        {
            thread = new Thread(DoFrame);
            thread.Start();
            System.getMasterChannelGroup(out FMOD.ChannelGroup parentGroup);
            parentGroup.setMute(false);
            State = State.Playing;
        }
        static void StopThread()
        {
            State = State.Stopped;
            if (thread != null && thread.IsAlive)
                thread.Join();
        }

        internal static (ushort, uint, uint[], sbyte[], sbyte[], sbyte[][], float[], byte[], byte[], int[], float[], string[]) GetSongState()
        {
            var offsets = new uint[NumTracks];
            var volumes = new sbyte[NumTracks];
            var delays = new sbyte[NumTracks];
            var notes = new sbyte[NumTracks][];
            var velocities = new float[NumTracks];
            var voices = new byte[NumTracks];
            var modulations = new byte[NumTracks];
            var bends = new int[NumTracks];
            var pans = new float[NumTracks];
            var types = new string[NumTracks];
            for (int i = 0; i < NumTracks; i++)
            {
                offsets[i] = Song.Commands[i][tracks[i].CommandIndex].Offset;
                volumes[i] = tracks[i].Volume;
                delays[i] = tracks[i].Delay;
                voices[i] = tracks[i].Voice;
                modulations[i] = tracks[i].MODDepth;
                bends[i] = tracks[i].Bend * tracks[i].BendRange;
                types[i] = VoiceTable[tracks[i].Voice].ToString();

                Instrument[] instruments = tracks[i].Instruments.Clone().ToArray();
                bool none = instruments.Length == 0;
                Instrument loudest = none ? null : instruments.OrderByDescending(ins => ins.Velocity).ElementAt(0);
                pans[i] = none ? tracks[i].Pan / 64f : loudest.Panpot;
                notes[i] = none ? new sbyte[0] : instruments.Where(ins => ins.State < ADSRState.Releasing).Select(ins => ins.DisplayNote).Distinct().ToArray();
                velocities[i] = none ? 0 : loudest.Velocity * (volumes[i] / 127f);
            }
            return ((ushort)(Tempo * 2), position, offsets, volumes, delays, notes, velocities, voices, modulations, bends, pans, types);
        }

        static void PlayInstrument(Track track, sbyte note, sbyte velocity, sbyte duration)
        {
            note = (sbyte)(note + track.KeyShift).Clamp(0, 127);

            if (!track.Ready) return;

            Voice voice = VoiceTable.GetVoiceFromNote(track.Voice, note, out bool fromDrum);

            Instrument instrument = null;
            switch (voice.Type)
            {
                case 0x0:
                case 0x8:
                    var byAge = dsInstruments.OrderByDescending(ins => (ins.Track == null ? 16 : ins.Track.Index)).ThenByDescending(ins => ins.Age);
                    foreach (Instrument i in byAge) // Find free
                        if (i.State == ADSRState.Dead)
                        {
                            instrument = i;
                            break;
                        }
                    if (instrument == null) // Find prioritized
                        foreach (Instrument i in byAge)
                            if (track.Priority > i.Track.Priority)
                            {
                                instrument = i;
                                break;
                            }
                    if (instrument == null) // Find releasing
                        foreach (Instrument i in byAge)
                            if (i.State == ADSRState.Releasing)
                            {
                                instrument = i;
                                break;
                            }
                    if (instrument == null) // None available
                    {
                        var lowestOldest = byAge.First(); // Kill lowest track's oldest instrument if the track is lower than this one
                        if (lowestOldest.Track.Index >= track.Index)
                            instrument = lowestOldest;
                    }
                    break;
                case 0x1:
                case 0x9:
                    instrument = gbInstruments[0];
                    break;
                case 0x2:
                case 0xA:
                    instrument = gbInstruments[1];
                    break;
                case 0x3:
                case 0xB:
                    instrument = gbInstruments[2];
                    break;
                case 0x4:
                case 0xC:
                    instrument = gbInstruments[3];
                    break;
            }

            if (instrument != null)
                instrument.Play(track, note, velocity, duration);
        }

        static void ExecuteNext(int i)
        {
            var track = tracks[i];
            var e = Song.Commands[i][track.CommandIndex];

            if (e.Command is GoToCommand goTo)
            {
                int gotoCmd = Song.Commands[i].FindIndex(c => c.Offset == goTo.Offset);
                position = Song.Commands[i][gotoCmd].AbsoluteTicks - 1;
                track.CommandIndex = gotoCmd - 1; // -1 for incoming ++
            }
            else if (e.Command is CallCommand patt)
            {
                int callCmd = Song.Commands[i].FindIndex(c => c.Offset == patt.Offset);
                track.EndOfPattern = track.CommandIndex;
                track.CommandIndex = callCmd - 1; // -1 for incoming ++
            }
            else if (e.Command is ReturnCommand)
            {
                if (track.EndOfPattern != 0)
                {
                    track.CommandIndex = track.EndOfPattern;
                    track.EndOfPattern = 0;
                }
            }
            else if (e.Command is FinishCommand) track.Stopped = true;
            else if (e.Command is PriorityCommand prio) track.SetPriority(prio.Priority);
            else if (e.Command is TempoCommand tempo) Tempo = tempo.Tempo;
            else if (e.Command is KeyShiftCommand keysh) track.KeyShift = keysh.Shift;
            else if (e.Command is NoteCommand n) PlayInstrument(track, n.Note, n.Velocity, n.Duration);
            else if (e.Command is RestCommand w) track.Delay = w.Rest;
            else if (e.Command is VoiceCommand voice) track.SetVoice(voice.Voice);
            else if (e.Command is VolumeCommand vol) track.SetVolume(vol.Volume);
            else if (e.Command is PanpotCommand pan) track.SetPan(pan.Panpot);
            else if (e.Command is BendCommand bend) track.SetBend(bend.Bend);
            else if (e.Command is BendRangeCommand bendr) track.SetBendRange(bendr.Range);
            else if (e.Command is LFOSpeedCommand lfos) track.SetLFOSpeed(lfos.Speed);
            else if (e.Command is LFODelayCommand lfodl) track.SetLFODelay(lfodl.Delay);
            else if (e.Command is ModDepthCommand mod) track.SetMODDepth(mod.Depth);
            else if (e.Command is ModTypeCommand modt) track.SetMODType((MODT)modt.Type);
            else if (e.Command is TuneCommand tune) track.SetTune(tune.Tune);
            else if (e.Command is EndOfTieCommand eot)
            {
                Instrument ins = null;
                if (eot.Note == -1)
                    ins = track.Instruments.LastOrDefault(inst => inst.NoteDuration == -1 && inst.State < ADSRState.Releasing);
                else
                {
                    byte note = (byte)(eot.Note + track.KeyShift).Clamp(0, 127);
                    ins = track.Instruments.LastOrDefault(inst => inst.NoteDuration == -1 && inst.DisplayNote == note && inst.State < ADSRState.Releasing);
                }
                if (ins != null)
                    ins.State = ADSRState.Releasing;
            }

            if (!track.Stopped)
                track.CommandIndex++;
        }
        static void DoFrame()
        {
            time.Start();
            while (State != State.Stopped)
            {
                // Do Song Tick
                tempoStack += Tempo * 2;
                while (tempoStack >= Constants.BPM_PER_FRAME * Constants.INTERFRAMES)
                {
                    tempoStack -= Constants.BPM_PER_FRAME * Constants.INTERFRAMES;
                    bool allDone = true;
                    for (int i = NumTracks - 1; i >= 0; i--)
                    {
                        Track track = tracks[i];
                        if (!track.Stopped || track.Instruments.Any(ins => ins.State != ADSRState.Dead))
                            allDone = false;
                        while (track.Delay == 0 && !track.Stopped)
                            ExecuteNext(i);
                        track.Tick();
                    }
                    position++;
                    if (allDone)
                    {
                        SongEnded?.Invoke();
                        Stop();
                    }
                }
                // Do Instrument Tick
                foreach (var i in allInstruments)
                    i.ADSRTick();
                System.update();
                // Wait for next frame
                time.Wait();
            }
            time.Stop();
        }
    }
}
