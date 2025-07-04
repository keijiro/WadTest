using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WadImporter
{
    [Serializable]
    public struct WadVertex
    {
        public short x;
        public short y;

        public Vector2 ToVector2() => new Vector2(x, y);

        public static WadVertex Read(BinaryReader reader)
        {
            return new WadVertex
            {
                x = reader.ReadInt16(),
                y = reader.ReadInt16()
            };
        }
    }

    [Serializable]
    public struct WadLinedef
    {
        public ushort startVertex;
        public ushort endVertex;
        public ushort flags;
        public ushort special;
        public ushort tag;
        public ushort frontSidedef;
        public ushort backSidedef;

        public bool HasBackSide => backSidedef != 0xFFFF;
        public bool TwoSided => (flags & 0x0004) != 0;
        public bool BlocksPlayer => (flags & 0x0001) != 0;

        public static WadLinedef Read(BinaryReader reader)
        {
            return new WadLinedef
            {
                startVertex = reader.ReadUInt16(),
                endVertex = reader.ReadUInt16(),
                flags = reader.ReadUInt16(),
                special = reader.ReadUInt16(),
                tag = reader.ReadUInt16(),
                frontSidedef = reader.ReadUInt16(),
                backSidedef = reader.ReadUInt16()
            };
        }
    }

    [Serializable]
    public struct WadSidedef
    {
        public short xOffset;
        public short yOffset;
        public string upperTexture;
        public string lowerTexture;
        public string middleTexture;
        public ushort sector;

        public static WadSidedef Read(BinaryReader reader)
        {
            return new WadSidedef
            {
                xOffset = reader.ReadInt16(),
                yOffset = reader.ReadInt16(),
                upperTexture = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0'),
                lowerTexture = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0'),
                middleTexture = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0'),
                sector = reader.ReadUInt16()
            };
        }
    }

    [Serializable]
    public struct WadSector
    {
        public short floorHeight;
        public short ceilingHeight;
        public string floorTexture;
        public string ceilingTexture;
        public short lightLevel;
        public short special;
        public short tag;

        public float FloorHeightUnits => floorHeight;
        public float CeilingHeightUnits => ceilingHeight;
        public float WallHeight => ceilingHeight - floorHeight;

        public static WadSector Read(BinaryReader reader)
        {
            return new WadSector
            {
                floorHeight = reader.ReadInt16(),
                ceilingHeight = reader.ReadInt16(),
                floorTexture = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0'),
                ceilingTexture = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0'),
                lightLevel = reader.ReadInt16(),
                special = reader.ReadInt16(),
                tag = reader.ReadInt16()
            };
        }
    }

    [Serializable]
    public struct WadThing
    {
        public short x;
        public short y;
        public ushort angle;
        public ushort type;
        public ushort flags;

        public Vector2 Position => new Vector2(x, y);

        public static WadThing Read(BinaryReader reader)
        {
            return new WadThing
            {
                x = reader.ReadInt16(),
                y = reader.ReadInt16(),
                angle = reader.ReadUInt16(),
                type = reader.ReadUInt16(),
                flags = reader.ReadUInt16()
            };
        }
    }

    [Serializable]
    public struct WadTexture
    {
        public string name;
        public ushort masked;
        public ushort width;
        public ushort height;
        public uint columnDirectory;
        public ushort patchCount;
        public WadPatch[] patches;

        public static WadTexture Read(BinaryReader reader)
        {
            var texture = new WadTexture
            {
                name = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0'),
                masked = reader.ReadUInt16(),
                width = reader.ReadUInt16(),
                height = reader.ReadUInt16(),
                columnDirectory = reader.ReadUInt32(),
                patchCount = reader.ReadUInt16()
            };

            texture.patches = new WadPatch[texture.patchCount];
            for (var i = 0; i < texture.patchCount; i++)
                texture.patches[i] = WadPatch.Read(reader);

            return texture;
        }
    }

    [Serializable]
    public struct WadPatch
    {
        public short originX;
        public short originY;
        public ushort patch;
        public ushort stepdir;
        public ushort colormap;

        public static WadPatch Read(BinaryReader reader)
        {
            return new WadPatch
            {
                originX = reader.ReadInt16(),
                originY = reader.ReadInt16(),
                patch = reader.ReadUInt16(),
                stepdir = reader.ReadUInt16(),
                colormap = reader.ReadUInt16()
            };
        }
    }

    [Serializable]
    public struct WadPatchHeader
    {
        public ushort width;
        public ushort height;
        public short leftOffset;
        public short topOffset;
        public uint[] columnOffsets;

        public static WadPatchHeader Read(BinaryReader reader)
        {
            var header = new WadPatchHeader
            {
                width = reader.ReadUInt16(),
                height = reader.ReadUInt16(),
                leftOffset = reader.ReadInt16(),
                topOffset = reader.ReadInt16()
            };

            header.columnOffsets = new uint[header.width];
            for (var i = 0; i < header.width; i++)
                header.columnOffsets[i] = reader.ReadUInt32();

            return header;
        }
    }

    public class LevelModel
    {
        public WadVertex[] vertices;
        public WadLinedef[] linedefs;
        public WadSidedef[] sidedefs;
        public WadSector[] sectors;
        public WadThing[] things;
        public Dictionary<string, WadTexture> textures;
        public Dictionary<string, byte[]> patches;
        public Color[] palette;
        public string[] pnames;

        public LevelModel()
        {
            vertices = new WadVertex[0];
            linedefs = new WadLinedef[0];
            sidedefs = new WadSidedef[0];
            sectors = new WadSector[0];
            things = new WadThing[0];
            textures = new Dictionary<string, WadTexture>();
            patches = new Dictionary<string, byte[]>();
            palette = new Color[0];
            pnames = new string[0];
        }
    }
}