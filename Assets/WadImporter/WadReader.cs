using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WadImporter
{
    public struct WadHeader
    {
        public string identification;
        public int lumpCount;
        public int directoryOffset;
    }

    public struct WadLump
    {
        public int offset;
        public int size;
        public string name;
    }

    public class WadReader
    {
        readonly byte[] data;
        readonly WadHeader header;
        readonly Dictionary<string, WadLump> lumps;
        List<string> lumpOrder;
        Dictionary<int, WadLump> lumpByIndex;

        public WadReader(byte[] wadData)
        {
            data = wadData;
            header = ReadHeader();
            lumps = ReadDirectory();
        }

        public bool HasLump(string name) => lumps.ContainsKey(name);

        public byte[] GetLumpData(string name)
        {
            if (!lumps.TryGetValue(name, out var lump))
                return null;

            var lumpData = new byte[lump.size];
            Array.Copy(data, lump.offset, lumpData, 0, lump.size);
            return lumpData;
        }

        public T ReadLumpData<T>(string name, Func<BinaryReader, T> reader)
        {
            var lumpData = GetLumpData(name);
            if (lumpData == null)
                return default(T);

            using (var stream = new MemoryStream(lumpData))
            using (var binaryReader = new BinaryReader(stream))
                return reader(binaryReader);
        }

        public T[] ReadLumpArray<T>(string name, Func<BinaryReader, T> itemReader)
        {
            var lumpData = GetLumpData(name);
            if (lumpData == null)
            {
                return new T[0];
            }

            // Calculate item size based on the actual data structure
            var itemSize = GetItemSize<T>();
            if (itemSize == 0)
            {
                Debug.LogError($"Unknown item size for type {typeof(T).Name}");
                return new T[0];
            }
            
            var itemCount = lumpData.Length / itemSize;
            // Debug.Log($"Reading lump '{name}': {lumpData.Length} bytes, {itemCount} items of size {itemSize}");
            var result = new T[itemCount];

            using (var stream = new MemoryStream(lumpData))
            using (var reader = new BinaryReader(stream))
            {
                for (var i = 0; i < itemCount; i++)
                    result[i] = itemReader(reader);
            }

            return result;
        }
        
        static int GetItemSize<T>()
        {
            var type = typeof(T);
            if (type == typeof(WadVertex)) return 4;      // 2 shorts
            if (type == typeof(WadLinedef)) return 14;    // 7 ushorts
            if (type == typeof(WadSidedef)) return 30;    // 2 shorts + 3 * 8 byte strings + 1 ushort
            if (type == typeof(WadSector)) return 26;     // 2 shorts + 2 * 8 byte strings + 3 shorts
            if (type == typeof(WadThing)) return 10;      // 2 shorts + 3 ushorts
            return 0;
        }

        public IEnumerable<string> GetLumpNames() => lumpOrder ?? lumps.Keys.ToList();
        
        public int GetLumpCount() => lumpOrder?.Count ?? lumps.Count;
        
        public int FindLumpIndex(string name)
        {
            return lumpOrder?.IndexOf(name) ?? -1;
        }
        
        public string GetLumpNameAtIndex(int index)
        {
            return (lumpOrder != null && index >= 0 && index < lumpOrder.Count) ? lumpOrder[index] : "";
        }
        
        public T[] ReadLumpArrayAtIndex<T>(int index, Func<BinaryReader, T> readFunc)
        {
            if (!lumpByIndex.ContainsKey(index))
                return new T[0];
                
            var lump = lumpByIndex[index];
            Debug.Log($"ReadLumpArrayAtIndex: Reading lump '{lump.name}' at index {index}, size: {lump.size}");
            
            var result = ReadLumpDirect(lump, readFunc);
            Debug.Log($"ReadLumpArrayAtIndex: Lump '{lump.name}' returned {result.Length} items");
            return result;
        }
        
        T[] ReadLumpDirect<T>(WadLump lump, Func<BinaryReader, T> readFunc)
        {
            if (lump.size == 0)
                return new T[0];

            var elementSize = GetItemSize<T>();
            if (elementSize == 0)
                return new T[0];

            var count = lump.size / elementSize;
            var result = new T[count];

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                stream.Seek(lump.offset, SeekOrigin.Begin);
                for (var i = 0; i < count; i++)
                    result[i] = readFunc(reader);
            }

            return result;
        }

        WadHeader ReadHeader()
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                var identification = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var lumpCount = reader.ReadInt32();
                var directoryOffset = reader.ReadInt32();

                return new WadHeader
                {
                    identification = identification,
                    lumpCount = lumpCount,
                    directoryOffset = directoryOffset
                };
            }
        }

        Dictionary<string, WadLump> ReadDirectory()
        {
            var result = new Dictionary<string, WadLump>();
            lumpOrder = new List<string>();
            lumpByIndex = new Dictionary<int, WadLump>();

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                stream.Seek(header.directoryOffset, SeekOrigin.Begin);

                for (var i = 0; i < header.lumpCount; i++)
                {
                    var offset = reader.ReadInt32();
                    var size = reader.ReadInt32();
                    var nameBytes = reader.ReadBytes(8);
                    var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

                    var lump = new WadLump
                    {
                        offset = offset,
                        size = size,
                        name = name
                    };

                    // Store by name (will overwrite duplicates)
                    result[name] = lump;
                    
                    // Store by index (preserves each unique lump)
                    lumpByIndex[i] = lump;
                    
                    lumpOrder.Add(name);
                }
            }

            return result;
        }
    }
}