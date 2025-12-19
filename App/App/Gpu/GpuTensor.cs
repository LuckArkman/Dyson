using System.Buffers;
using System.Runtime.InteropServices;
using Core;
using Interfaces;
using OpenCL.NetCore;
using static OpenCL.NetCore.Cl;

namespace Gpu;

public class GpuTensor : IMathTensor
    {
        public int[] Shape { get; }
        public long Length { get; }
        public bool IsGpu => true;

        internal Mem Buffer { get; private set; }
        private static readonly ArrayPool<float> IoFloatBufferPool = ArrayPool<float>.Shared;
        private readonly Context _context;
        private readonly CommandQueue _commandQueue;
        private readonly GpuSyncGuard _syncGuard;
        private bool _disposed = false;
        private readonly object _syncLock = new object();

        public GpuTensor(int[] shape, Context context, CommandQueue commandQueue, GpuSyncGuard syncGuard)
        {
            Shape = shape;
            Length = shape.Aggregate(1L, (a, b) => a * b);
            _context = context;
            _commandQueue = commandQueue;
            _syncGuard = syncGuard;

            ErrorCode error;
            Buffer = (Mem)CreateBuffer(_context, MemFlags.ReadWrite, (IntPtr)(Length * sizeof(float)), IntPtr.Zero, out error);
            if (error != ErrorCode.Success) throw new OpenClException("Falha alocação GPU", error);
        }

        public GpuTensor(float[] data, int[] shape, Context context, CommandQueue commandQueue, GpuSyncGuard syncGuard)
        {
            Shape = shape;
            Length = data.Length;
            _context = context;
            _commandQueue = commandQueue;
            _syncGuard = syncGuard;

            ErrorCode error;
            Buffer = (Mem)CreateBuffer(_context, MemFlags.ReadWrite | MemFlags.CopyHostPtr, (IntPtr)(Length * sizeof(float)), data, out error);
            if (error != ErrorCode.Success) throw new OpenClException("Falha alocação GPU (Copy)", error);
        }

        public Tensor ToCpuTensor()
        {
            lock (_syncLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GpuTensor));
                _syncGuard.SynchronizeBeforeRead("ToCpu");
                var data = new float[Length];
                EnqueueReadBuffer(_commandQueue, Buffer, Bool.True, IntPtr.Zero, (IntPtr)(Length * sizeof(float)), data, 0, null, out _);
                return new Tensor(data, Shape);
            }
        }

        public void UpdateFromCpu(float[] data)
        {
            lock (_syncLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GpuTensor));
                _syncGuard.SynchronizeBeforeRead("UpdateCpu");
                EnqueueWriteBuffer(_commandQueue, Buffer, Bool.True, IntPtr.Zero, (IntPtr)(Length * sizeof(float)), data, 0, null, out _);
            }
        }

        public unsafe void WriteToStream(BinaryWriter writer)
        {
            lock (_syncLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GpuTensor));

                int byteSize = (int)(Length * sizeof(float));
                float[] hostBuffer = IoFloatBufferPool.Rent((int)Length);
                try
                {
                    _syncGuard.SynchronizeBeforeRead("WriteStream");
                    GCHandle handle = GCHandle.Alloc(hostBuffer, GCHandleType.Pinned);
                    try
                    {
                        IntPtr hostPtr = handle.AddrOfPinnedObject();
                        // CORREÇÃO: Força leitura bloqueante para garantir integridade antes da escrita
                        EnqueueReadBuffer(_commandQueue, Buffer, Bool.True, IntPtr.Zero, (IntPtr)byteSize, hostPtr, 0, null, out _);
                        Finish(_commandQueue); // Garantia extra
                        
                        var byteSpan = MemoryMarshal.AsBytes(hostBuffer.AsSpan(0, (int)Length));
                        writer.Write(byteSpan);
                    }
                    finally { if (handle.IsAllocated) handle.Free(); }
                }
                finally { IoFloatBufferPool.Return(hostBuffer); }
            }
        }

        public unsafe void ReadFromStream(BinaryReader reader, long length)
        {
            lock (_syncLock)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(GpuTensor));
                int byteSize = (int)(length * sizeof(float));
                byte[] byteBuffer = reader.ReadBytes(byteSize);
                float[] hostBuffer = new float[length];
                MemoryMarshal.Cast<byte, float>(byteBuffer.AsSpan()).CopyTo(hostBuffer.AsSpan());
                
                _syncGuard.SynchronizeBeforeRead("ReadStream");
                EnqueueWriteBuffer(_commandQueue, Buffer, Bool.True, IntPtr.Zero, (IntPtr)byteSize, hostBuffer, 0, null, out _);
                Finish(_commandQueue);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            lock (_syncLock)
            {
                if (!Buffer.Equals(default(Mem)))
                {
                    _syncGuard.SynchronizeBeforeDispose("Dispose", Length * 4);
                    ReleaseMemObject(Buffer);
                    Buffer = default(Mem);
                }
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~GpuTensor()
        {
            if (!_disposed) Dispose();
        }
    }