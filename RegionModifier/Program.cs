using Substrate;
using Substrate.Core;
using Substrate.Nbt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RegionModifier
{
    internal class Program
    {
        const string worldPath = @"[enter a path here]";
        const string blockIDPrefix = "";
        static void Main(string[] args)
        {
            try
            {
                if (!Directory.Exists(worldPath))
                {
                    throw new DirectoryNotFoundException("worldPath needs to be filled in in code.");
                }

                if (string.IsNullOrEmpty(blockIDPrefix))
                {
                    throw new ArgumentException("blockIDPrefix needs to be filled in in code.");
                }

                NbtWorld world = NbtWorld.Open(worldPath);

                NBTFile file = new NBTFile(world.Path + "/level.dat");
                NbtTree levelTree = new NbtTree(file.GetDataInputStream());
                
                TagNodeCompound fml = levelTree.Root.GetOrDefault("FML") as TagNodeCompound;
                if (fml == null) return;

                TagNodeCompound registries = fml.GetOrDefault("Registries") as TagNodeCompound;
                if (registries == null) return;

                TagNodeCompound blockRegistry = registries.GetOrDefault("minecraft:blocks") as TagNodeCompound;
                if (blockRegistry == null) return;

                TagNodeList blockIDs = blockRegistry.GetOrDefault("ids") as TagNodeList;
                if (blockIDs == null) return;

                HashSet<int> blockIDsFound = new HashSet<int>();
                Dictionary<int, string> blockNamesByID = new Dictionary<int, string>();
                foreach(TagNode tagNode in blockIDs)
                {
                    TagNodeCompound idEntry = tagNode as TagNodeCompound;
                    if (idEntry == null) continue;

                    TagNodeString id = idEntry.GetOrDefault("K") as TagNodeString;
                    TagNodeInt value = idEntry.GetOrDefault("V") as TagNodeInt;
                    if (id == null || value == null) continue;

                    if (id.Data.StartsWith(blockIDPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        blockIDsFound.Add(value.Data);
                    }

                    blockNamesByID.Add(value.Data, id.Data);
                }

                if (blockIDsFound.Count == 0)
                {
                    Console.WriteLine("No block found");
                    return;
                }

                Console.WriteLine("Found {0} block ids", blockIDsFound.Count);

                string[] files = Directory.GetFiles(Path.Combine(worldPath, "region"));
                for (int i = 0; i < files.Length; i++)
                {
                    Console.WriteLine("Searching region {0}/{1} ({2})", i, files.Length, Path.GetFileName(files[i]));

                    NbtTree nbtTree;
                    using (RegionFile regionFile = new RegionFile(files[i]))
                    {
                        string regionFileName = Path.GetFileName(files[i]);
                        string[] filePathParts = regionFileName.Split('.');
                        int regX = int.Parse(filePathParts[1]);
                        int regZ = int.Parse(filePathParts[2]);

                        for (int cx = 0; cx < 32; cx++)
                        {
                            for (int cz = 0; cz < 32; cz++)
                            {
                                if (!regionFile.HasChunk(cx, cz)) continue;

                                int chunkX = cx * 16 + (512 * regX);
                                int chunkZ = (regZ * 512) + cz * 16;

                                bool chunkChanges = false;

                                nbtTree = new NbtTree(regionFile.GetChunkDataInputStream(cx, cz));
                                if (nbtTree?.Root == null) continue;

                                TagNodeCompound level = nbtTree.Root.GetOrDefault("Level") as TagNodeCompound;
                                if (level == null) return;

                                TagNodeList sections = level.GetOrDefault("Sections") as TagNodeList;
                                if (sections == null) return;

                                for (int j = 0; j < sections.Count; j++)
                                {
                                    TagNodeCompound section = sections[j] as TagNodeCompound;
                                    if (section == null) continue;

                                    byte[] blocks = (section.GetOrDefault("Blocks") as TagNodeByteArray)?.Data;

                                    NibbleArray dataNibble = null;
                                    byte[] data = (section.GetOrDefault("Data") as TagNodeByteArray)?.Data;
                                    if (data != null)
                                    {
                                        dataNibble = new NibbleArray(data);
                                    }

                                    NibbleArray blockIDExtensionNibble = null;
                                    byte[] addArray = (section.GetOrDefault("Add") as TagNodeByteArray)?.Data;
                                    if (addArray != null)
                                    {
                                        blockIDExtensionNibble = new NibbleArray(addArray);
                                    }

                                    int[] palette = (section.GetOrDefault("Palette") as TagNodeIntArray)?.Data;

                                    //if (palette == null)
                                    //{
                                    //    for (int k = 0; k < 4096; ++k)
                                    //    {
                                    //        int blockPosX = k & 15;
                                    //        int blockPosY = k >> 8 & 15;
                                    //        int blockPosZ = k >> 4 & 15;

                                    //        int blockIDExtension = blockIDExtensionNibble == null ? 0 : Get(blockIDExtensionNibble, blockPosX, blockPosY, blockPosZ);
                                    //        int stateID = blockIDExtension << 12 | (blocks[k] & 255) << 4 | Get(dataNibble, blockPosX, blockPosY, blockPosZ);

                                    //        int blockID = (stateID & 4095) >> 4;
                                    //        if (blockIDsFound.Contains(blockID))
                                    //        {
                                    //            Console.WriteLine("Found it in blocks/data! It's a " + blockNamesByID.GetOrDefault(blockID));
                                    //            File.AppendAllLines("results.txt", new[] { "Found it in blocks/data! It's a " + blockNamesByID.GetOrDefault(blockID) + ". http://map.mesabrook.com/#world;flat;" + (chunkX + blockPosX) + ",64," + (chunkZ + blockPosZ) + ";6" });
                                    //        }
                                    //    }
                                    //}
                                    //else
                                    if (palette != null)
                                    {
                                        for (int k = 0; k < 4096; ++k)
                                        {
                                            int blockPosX = k & 15;
                                            int blockPosY = k >> 8 & 15;
                                            int blockPosZ = k >> 4 & 15;

                                            int stateID = (blocks[k] & 255) << 4 | Get(dataNibble, blockPosX, blockPosY, blockPosZ);
                                            stateID = palette[stateID];

                                            int blockID = stateID >> 4;

                                            if (blockIDsFound.Contains(blockID))
                                            {
                                                Console.WriteLine("Found it in pallete! It's a " + blockNamesByID.GetOrDefault(blockID));
                                                //File.AppendAllLines("results.txt", new[] { "Found it in pallete! It's a " + blockNamesByID.GetOrDefault(blockID) + ". http://map.mesabrook.com/#world;flat;" + (chunkX + blockPosX) + ",64," + (chunkZ + blockPosZ) + ";6" });

                                                if (!palette.Any(pid => pid == 0))
                                                {
                                                    int[] tempPalette = new int[palette.Length + 1];
                                                    Array.Copy(palette, tempPalette, palette.Length);
                                                    tempPalette[tempPalette.Length - 1] = 0;
                                                    palette = tempPalette;
                                                    section["Palette"].ToTagIntArray().Data = palette;
                                                    blocks[k] = (byte)((tempPalette.Length - 1) >> 4 & 255);
                                                    Set(dataNibble, blockPosX, blockPosY, blockPosZ, (tempPalette.Length - 1) & 15);
                                                }
                                                else
                                                {
                                                    int airIndex = Array.IndexOf(palette, 0);
                                                    blocks[k] = (byte)(airIndex >> 4 & 255);
                                                    Set(dataNibble, blockPosX, blockPosY, blockPosZ, airIndex & 15);
                                                }

                                                chunkChanges = true;
                                            }
                                        }
                                    }

                                    if (chunkChanges)
                                    {
                                        using (Stream stream = regionFile.GetChunkDataOutputStream(cx, cz))
                                        {
                                            nbtTree.WriteTo(stream);
                                            stream.Flush();
                                            stream.Close();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
        }

        private static int Get(NibbleArray nibble, int x, int y, int z)
        {
            return nibble[y << 8 | z << 4 | x];
        }

        private static void Set(NibbleArray nibble, int x, int y, int z, int value)
        {
            nibble[y << 8 | z << 4 | x] = value;
        }
    }
}
