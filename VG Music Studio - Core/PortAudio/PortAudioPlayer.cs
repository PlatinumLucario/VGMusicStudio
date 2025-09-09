using System;
using System.Runtime.InteropServices;
using Kermalis.VGMusicStudio.Core;
using Kermalis.VGMusicStudio.Core.Formats;

namespace PortAudio;

public enum CallbackState
{
    Stop,
    Start,
    Pause
}

public class PortAudioPlayer
{
    public static CallbackState? CallbackState { get; protected set; }
    private static int ReadPos = 0;

    private static Span<sbyte> CastBytesToSBytes(Span<byte> byteMem)
    {
        return MemoryMarshal.Cast<byte, sbyte>(byteMem);
    }

    private static Span<short> CastBytesToShorts(Span<byte> byteMem)
    {
        return MemoryMarshal.Cast<byte, short>(byteMem);
    }

    private static Span<int> CastBytesToInts(Span<byte> byteMem)
    {
        return MemoryMarshal.Cast<byte, int>(byteMem);
    }

    private static Span<Int24> CastBytesToInt24s(Span<byte> byteMem)
    {
        return MemoryMarshal.Cast<byte, Int24>(byteMem);
    }

    private static Span<float> CastBytesToFloats(Span<byte> byteMem)
    {
        return MemoryMarshal.Cast<byte, float>(byteMem);
    }

    internal static StreamCallbackResult Play(
        nint input, nint output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        nint userData
    )
    {
        // Marshal.AllocHGlobal() or any related functions cannot and must not be used
        // in this callback, otherwise it will cause an OutOfMemoryException.
        //
        // The memory is already allocated by the output and userData params by
        // the PortAudio bindings.

        if (Engine.Instance is null)
        {
            return StreamCallbackResult.Continue;
        }

        var mixer = Engine.Instance!.Mixer;

        if (Engine.Instance.Mixer.Stream is null)
        {
            ReadPos = 0;
            return StreamCallbackResult.Abort;
        }

        Wave d = Engine.Instance.Mixer!.Stream!.GetUserData<Wave>(userData);
        if (d.Buffer is null)
        {
            ReadPos = 0;
            return StreamCallbackResult.Continue;
        }

        if (!Engine.Instance.Mixer.Stream.UDHandle.IsAllocated)
        {
            ReadPos = 0;
            return StreamCallbackResult.Abort;
        }

        // RealignBufferPos(d);

        Option1(Engine.Instance.Player, d, output, frameCount);

        while (d.BufferState is BufferState.Writing)
        {
            CallbackState = PortAudio.CallbackState.Pause;
            var allocatedReadPos = ReadPos + frameCount;
            var allocatedWritePos = d.WritePosition + mixer.SamplesPerBuffer;
            if (allocatedReadPos >= d.WritePosition && allocatedReadPos < allocatedWritePos)
            {
                if ((ReadPos + mixer.SamplesPerBuffer) > d.BufferLength)
                {
                    ReadPos = d.WritePosition - mixer.SamplesPerBuffer;
                }
                else
                {
                    ReadPos = d.WritePosition + mixer.SamplesPerBuffer;
                }
            }
        }

        CallbackState = PortAudio.CallbackState.Start;

        if (!Engine.Instance!.Mixer!.IsDisposing)
        {
            // Continue if the mixer isn't being disposed
            return StreamCallbackResult.Continue;
        }
        else
        {
            // Complete the callback if the mixer is being disposed
            d.ResetBuffer();
            ReadPos = 0;
            Engine.Instance!.Mixer!.IsDisposing = false;
            return StreamCallbackResult.Complete;
        }
    }

    private static void Option1(Player player, Wave d, nint output, uint frameCount)
    {

        switch (Engine.Instance!.Mixer!.OParams.SampleFormat)
        {
            case SampleFormat.UInt8:
                {
                    Span<byte> buffer;
                    unsafe
                    {
                        // Apply buffer value
                        buffer = new Span<byte>((byte*)output, (int)(frameCount * 2));
                    }

                    ReadPos %= d.Buffer!.Length;

                    // If we're reading data, play it back
                    if (player.State == PlayerState.Playing)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (ReadPos + i >= buffer.Length)
                            {
                                break;
                            }
                            buffer[i] = (byte)(d.Buffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
                        }
                    }
                    else
                    {
                        buffer.Clear();
                    }

                    ReadPos += buffer.Length;

                    if (ReadPos >= d.Buffer.Length)
                    {
                        ReadPos = 0;
                    }

                    if (Engine.Instance!.Mixer!.IsDisposing)
                    {
                        buffer.Clear();
                    }

                    break;
                }
            case SampleFormat.Int8:
                {
                    Span<sbyte> buffer;
                    Span<sbyte> waveBuffer = CastBytesToSBytes(d.Buffer);
                    unsafe
                    {
                        // Apply buffer value
                        buffer = new Span<sbyte>((sbyte*)output, (int)(frameCount * 2));
                    }

                    ReadPos %= waveBuffer.Length;

                    // If we're reading data, play it back
                    if (player.State == PlayerState.Playing)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (ReadPos + i >= waveBuffer.Length)
                            {
                                break;
                            }
                            buffer[i] = (sbyte)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
                        }
                    }
                    else
                    {
                        buffer.Clear();
                    }

                    ReadPos += buffer.Length;

                    if (ReadPos >= waveBuffer.Length)
                    {
                        ReadPos = 0;
                    }

                    if (Engine.Instance!.Mixer!.IsDisposing)
                    {
                        buffer.Clear();
                    }

                    break;
                }
            case SampleFormat.Int16:
                {
                    Span<short> buffer;
                    Span<short> waveBuffer = CastBytesToShorts(d.Buffer);
                    unsafe
                    {
                        // Apply buffer value
                        buffer = new Span<short>((short*)output, (int)(frameCount * 2));
                    }

                    ReadPos %= waveBuffer.Length;

                    // If we're reading data, play it back
                    if (player.State == PlayerState.Playing)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (ReadPos + i >= waveBuffer.Length)
                            {
                                break;
                            }
                            buffer[i] = (short)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
                        }
                    }
                    else
                    {
                        buffer.Clear();
                    }

                    ReadPos += buffer.Length;

                    if (ReadPos >= waveBuffer.Length)
                    {
                        ReadPos = 0;
                    }

                    if (Engine.Instance!.Mixer!.IsDisposing)
                    {
                        buffer.Clear();
                    }

                    break;
                }
            case SampleFormat.Int24:
                {
                    Span<Int24> buffer;
                    Span<Int24> waveBuffer = CastBytesToInt24s(d.Buffer);
                    unsafe
                    {
                        // Apply buffer value
                        buffer = new Span<Int24>((Int24*)output, (int)(frameCount * 2));
                    }

                    ReadPos %= waveBuffer.Length;

                    // If we're reading data, play it back
                    if (player.State == PlayerState.Playing)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (ReadPos + i >= waveBuffer.Length)
                            {
                                break;
                            }
                            buffer[i] = (Int24)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
                        }
                    }
                    else
                    {
                        buffer.Clear();
                    }

                    ReadPos += buffer.Length;

                    if (ReadPos >= waveBuffer.Length)
                    {
                        ReadPos = 0;
                    }

                    if (Engine.Instance!.Mixer!.IsDisposing)
                    {
                        buffer.Clear();
                    }

                    break;
                }
            case SampleFormat.Int32:
                {
                    Span<int> buffer;
                    Span<int> waveBuffer = CastBytesToInts(d.Buffer);
                    unsafe
                    {
                        // Apply buffer value
                        buffer = new Span<int>((int*)output, (int)(frameCount * 2));
                    }

                    ReadPos %= waveBuffer.Length;

                    // If we're reading data, play it back
                    if (player.State == PlayerState.Playing)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (ReadPos + i >= waveBuffer.Length)
                            {
                                break;
                            }
                            buffer[i] = (int)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
                        }
                    }
                    else
                    {
                        buffer.Clear();
                    }

                    ReadPos += buffer.Length;

                    if (ReadPos >= waveBuffer.Length)
                    {
                        ReadPos = 0;
                    }

                    if (Engine.Instance!.Mixer!.IsDisposing)
                    {
                        buffer.Clear();
                    }

                    break;
                }
            case SampleFormat.Float32:
                {
                    Span<float> buffer;
                    Span<float> waveBuffer = CastBytesToFloats(d.Buffer);
                    unsafe
                    {
                        // Apply buffer value
                        buffer = new Span<float>((float*)output, (int)(frameCount * 2));
                    }

                    ReadPos %= waveBuffer.Length;

                    // If we're reading data, play it back
                    if (player.State == PlayerState.Playing)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            if (ReadPos + i >= waveBuffer.Length)
                            {
                                break;
                            }
                            buffer[i] = (float)(waveBuffer[ReadPos + i] * Engine.Instance.Mixer.Volume);
                        }
                    }
                    else
                    {
                        buffer.Clear();
                    }

                    ReadPos += buffer.Length;

                    if (ReadPos >= waveBuffer.Length)
                    {
                        ReadPos = 0;
                    }

                    if (Engine.Instance!.Mixer!.IsDisposing)
                    {
                        buffer.Clear();
                    }

                    break;
                }
        }
    }

    // // Experimental realignment func to prevent reading from buffers being written to
    // protected static void RealignBufferPos(Wave waveData)
    // {
    // 	var count = waveData.Count / 4;
    // 	var writePos = waveData.WritePosition / 4;

    // 	if (writePos - count < 0)
    // 	{
    // 		if (ReadPos.Equals((writePos - count + (waveData.BufferLength / 4))..^(waveData.BufferLength / 4)))
    // 		{
    // 			if (ReadPos < writePos)
    // 			{
    // 				ReadPos -= count;
    // 				if (ReadPos <= 0)
    // 				{
    // 					ReadPos += waveData.BufferLength / 4;
    // 				}
    // 			}
    // 			else
    // 			{
    // 				ReadPos += count;
    // 				if (ReadPos + count >= (waveData.BufferLength / 4))
    // 				{
    // 					ReadPos -= waveData.BufferLength / 4;
    // 				}
    // 			}
    // 		}
    // 		else if (ReadPos.Equals(writePos..^(writePos + count)))
    // 		{
    // 			if (ReadPos < writePos)
    // 			{
    // 				ReadPos -= count;
    // 				if (ReadPos <= 0)
    // 				{
    // 					ReadPos += waveData.BufferLength / 4;
    // 				}
    // 			}
    // 			else
    // 			{
    // 				ReadPos += count;
    // 				if (ReadPos + count >= (waveData.BufferLength / 4))
    // 				{
    // 					ReadPos -= waveData.BufferLength / 4;
    // 				}
    // 			}
    // 		}
    // 	}
    // 	if (writePos > count && writePos < (waveData.BufferLength / 4))
    // 	{
    // 		if (ReadPos.Equals((writePos - count)..^(writePos + count)))
    // 		{
    // 			if (ReadPos < writePos)
    // 			{
    // 				ReadPos -= count;
    // 				if (ReadPos <= 0)
    // 				{
    // 					ReadPos += waveData.BufferLength / 4;
    // 				}
    // 			}
    // 			else
    // 			{
    // 				ReadPos += count;
    // 				if (ReadPos + count >= (waveData.BufferLength / 4))
    // 				{
    // 					ReadPos -= waveData.BufferLength / 4;
    // 				}
    // 			}
    // 		}
    // 	}
    // }
}
