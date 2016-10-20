using System;
using System.IO;

namespace RomVaultX.SupportedFiles.SevenZip.Structure
{
	public class Folder
	{


		public Coder[] Coders;
		public BindPair[] BindPairs;
		public ulong[] PackedStreamIndices;
		public ulong[] UnpackedStreamSizes;
		public uint? UnpackCRC;
		public UnpackedStreamInfo[] UnpackedStreamInfo;



		private void ReadFolder(BinaryReader br)
		{
			Util.log("Begin : Read Folder", 1);

			ulong numCoders = br.ReadEncodedUInt64();
			Util.log("NumCoders = " + numCoders);

			Coders = new Coder[numCoders];

			int numInStreams = 0;
			int numOutStreams = 0;

			Util.log("Looping Coders Begin", 1);
			for (ulong i = 0; i < numCoders; i++)
			{
				Util.log("Looping Coders : " + i);
				Coders[i] = new Coder();
				Coders[i].Read(br);

				numInStreams += (int)Coders[i].NumInStreams;
				numOutStreams += (int)Coders[i].NumOutStreams;
			}
			Util.log("Looping Coders End", -1);

			int numBindPairs = numOutStreams - 1;
			Util.log("numBindPairs : " + numBindPairs);
			BindPairs = new BindPair[numBindPairs];

			Util.log("Looping BindPairs Begin", 1);
			for (int i = 0; i < numBindPairs; i++)
			{
				Util.log("Looping BindPairs : " + i);
				BindPairs[i] = new BindPair();
				BindPairs[i].Read(br);
			}
			Util.log("Looping BindPairs End", -1);

			if (numInStreams < numBindPairs)
				throw new NotSupportedException("Error");

			int numPackedStreams = numInStreams - numBindPairs;
			Util.log("numPackedStreams : " + numPackedStreams);

			PackedStreamIndices = new ulong[numPackedStreams];

			if (numPackedStreams == 1)
			{
				uint pi = 0;
				for (uint j = 0; j < numInStreams; j++)
				{
					for (uint k = 0; k < BindPairs.Length; k++)
					{
						if (BindPairs[k].InIndex == j) continue;
						Util.log("PackedStreamIndices[" + pi + "] : " + j);
						PackedStreamIndices[pi++] = j;
						break;
					}
				}
			}
			else
			{
				for (uint i = 0; i < numPackedStreams; i++)
				{
					PackedStreamIndices[i] = br.ReadEncodedUInt64();
					Util.log("PackedStreamIndices[" + i + "] = " + PackedStreamIndices[i]);
				}
			}

			Util.log("End : Read Folder", -1);
		}
		private void ReadUnpackedStreamSize(BinaryReader br)
		{
			ulong outStreams = 0;
			foreach (Coder c in Coders)
				outStreams += c.NumOutStreams;

			Util.log("Looping UnpackedStreamSizes Begin", 1);
			UnpackedStreamSizes = new ulong[outStreams];
			for (uint j = 0; j < outStreams; j++)
			{
				UnpackedStreamSizes[j] = br.ReadEncodedUInt64();
				Util.log("unpackedStreamSizes[" + j + "] = " + UnpackedStreamSizes[j]);
			}
			Util.log("Looping UnpackedStreamSizes End", -1);
		}
		private ulong GetUnpackSize()
		{
			ulong outStreams = 0;
			foreach (Coder coder in Coders)
				outStreams += coder.NumInStreams;

			for (ulong j = 0; j < outStreams; j++)
			{
				bool found = false;
				foreach (BindPair bindPair in BindPairs)
				{
					if (bindPair.OutIndex != j)
						continue;
					found = true;
					break;
				}
				if (!found) return UnpackedStreamSizes[j];

			}
			return 0;
		}


		public static void ReadUnPackInfo(BinaryReader br, out Folder[] Folders)
		{
			Folders = null;
			Util.log("Begin : ReadUnPackInfo", 1);

			for (;;)
			{
				HeaderProperty hp = (HeaderProperty)br.ReadByte();
				Util.log("HeaderProperty = " + hp);
				switch (hp)
				{
					case HeaderProperty.kFolder:
						{
							ulong numFolders = br.ReadEncodedUInt64();
							Util.log("NumFolders = " + numFolders);

							Folders = new Folder[numFolders];

							byte external = br.ReadByte();
							Util.log("External = " + external);
							switch (external)
							{
								case 0:
									{
										Util.log("Looping Folders Begin", 1);
										for (uint i = 0; i < numFolders; i++)
										{
											Util.log("Looping Folders : " + i);
											Folders[i] = new Folder();
											Folders[i].ReadFolder(br);
										}
										Util.log("Looping Folders End", -1);
										break;
									}
								case 1:
									throw new NotSupportedException("External flag");
							}
							continue;
						}


					case HeaderProperty.kCodersUnPackSize:
						{
							Util.log("Looping Folders Begin", 1);
							for (uint i = 0; i < Folders.Length; i++)
							{
								Util.log("Looping Folders : " + i);
								Folders[i].ReadUnpackedStreamSize(br);
							}
							Util.log("Looping Folders End", -1);
							continue;
						}

					case HeaderProperty.kCRC:
						{

							uint?[] crcs;
							Util.log("Looping CRC Begin", 1);
							Util.UnPackCRCs(br, (ulong)Folders.Length, out crcs);
							for (int i = 0; i < Folders.Length; i++)
							{
								Folders[i].UnpackCRC = crcs[i];
								Util.log("Folder[" + i + "].UnpackCRC = " + (Folders[i].UnpackCRC ?? 0).ToString("X"));
							}
							Util.log("Looping CRC End", -1);
							continue;
						}
					case HeaderProperty.kEnd:
						Util.log("End : ReadUnPackInfo", -1);
						return;

					default:
						throw new Exception(hp.ToString());
				}


			}
		}

		public static void ReadSubStreamsInfo(BinaryReader br, ref Folder[] Folders)
		{
			Util.log("Begin : ReadSubStreamsInfo", 1);

			for (;;)
			{
				HeaderProperty hp = (HeaderProperty)br.ReadByte();
				Util.log("HeaderProperty = " + hp);
				switch (hp)
				{
					case HeaderProperty.kNumUnPackStream:
						{
							Util.log("Looping Folders Begin", 1);
							for (int f = 0; f < Folders.Length; f++)
							{
								int numStreams = (int)br.ReadEncodedUInt64();
								Util.log("Folders[" + f + "].Length=" + numStreams);
								Folders[f].UnpackedStreamInfo = new UnpackedStreamInfo[numStreams];
								for (int i = 0; i < numStreams; i++)
									Folders[f].UnpackedStreamInfo[i] = new UnpackedStreamInfo();
							}
							Util.log("Looping Folders End", -1);
							continue;
						}
					case HeaderProperty.kSize:
						{
							Util.log("Looping Folders Begin", 1);
							for (int f = 0; f < Folders.Length; f++)
							{
								Folder folder = Folders[f];

								if (folder.UnpackedStreamInfo.Length == 0)
								{
									Util.log("Folder size is Zero for " + f + " Begin");
									continue;
								}

								Util.log("Looping Folder UnpackedStreams" + f + " : " + (folder.UnpackedStreamInfo.Length - 1), 1);
								ulong sum = 0;
								for (int i = 0; i < folder.UnpackedStreamInfo.Length - 1; i++)
								{
									ulong size = br.ReadEncodedUInt64();
									folder.UnpackedStreamInfo[i].UnpackedSize = size;
									Util.log("UnpackedStreams[" + i + "].UnpackedSize = " + folder.UnpackedStreamInfo[i].UnpackedSize);
									sum += size;
								}
								Util.log("Looping Folder UnpackedStreams " + f + " End", -1);

								Util.log("Sum : " + sum + " : Folder[" + f + "].GetUnpackSize()=" + Folders[f].GetUnpackSize());
								folder.UnpackedStreamInfo[folder.UnpackedStreamInfo.Length - 1].UnpackedSize = folder.GetUnpackSize() - sum;
								Util.log("UnpackedStreams[" + (folder.UnpackedStreamInfo.Length - 1) + "].UnpackedSize = " + folder.UnpackedStreamInfo[folder.UnpackedStreamInfo.Length - 1].UnpackedSize);
							}
							Util.log("Looping Folders End", -1);
							continue;
						}
					case HeaderProperty.kCRC:
						{
							ulong numCRC = 0;
							foreach (var folder in Folders)
							{
								if (folder.UnpackedStreamInfo == null)
								{
									folder.UnpackedStreamInfo = new UnpackedStreamInfo[1];
									folder.UnpackedStreamInfo[0] = new UnpackedStreamInfo();
									folder.UnpackedStreamInfo[0].UnpackedSize = folder.GetUnpackSize();
								}

								if (folder.UnpackedStreamInfo.Length != 1 || !folder.UnpackCRC.HasValue)
									numCRC += (ulong)folder.UnpackedStreamInfo.Length;
							}

							Util.log("Reading CRC Total : " + numCRC);
							int crcIndex = 0;
							uint?[] crc;
							Util.UnPackCRCs(br, numCRC, out crc);
							Util.log("Looping Folders Begin", 1);
							for (uint i = 0; i < Folders.Length; i++)
							{
								Folder folder = Folders[i];
								if (folder.UnpackedStreamInfo.Length == 1 && folder.UnpackCRC.HasValue)
								{
									folder.UnpackedStreamInfo[0].Crc = folder.UnpackCRC;
									Util.log("UnpackedStreams[0].Crc = " + folder.UnpackedStreamInfo[0].Crc);
								}
								else
								{
									Util.log("Looping Folder UnpackedStreams" + i + " : " + (Folders[i].UnpackedStreamInfo.Length), 1);
									for (uint j = 0; j < folder.UnpackedStreamInfo.Length; j++, crcIndex++)
									{
										folder.UnpackedStreamInfo[j].Crc = crc[crcIndex];
										Util.log("UnpackedStreams[" + j + "].Crc = " + (folder.UnpackedStreamInfo[j].Crc ?? 0).ToString("X"));
									}
									Util.log("Looping Folder UnpackedStreams " + i + " End", -1);

								}
							}
							Util.log("Looping Folders End", -1);

							continue;
						}
					case HeaderProperty.kEnd:
						Util.log("End : ReadSubStreamsInfo", -1);
						return;

					default:
						throw new Exception(hp.ToString());
				}
			}
		}

		private void WriteFolder(BinaryWriter bw)
		{
			ulong numCoders = (ulong)Coders.Length;
			bw.WriteEncodedUInt64(numCoders);
			for (ulong i = 0; i < numCoders; i++)
				Coders[i].Write(bw);

			ulong numBindingPairs = BindPairs == null ? 0 : (ulong)BindPairs.Length;
			for (ulong i = 0; i < numBindingPairs; i++)
				BindPairs[i].Write(bw);

			//need to look at PAckedStreamIndices but don't need them for basic writing I am doing
		}
		private void WriteUnpackedStreamSize(BinaryWriter bw)
		{
			ulong numUnpackedStreamSizes = (ulong)UnpackedStreamSizes.Length;
			for (ulong i = 0; i < numUnpackedStreamSizes; i++)
				bw.WriteEncodedUInt64(UnpackedStreamSizes[i]);
		}

		public static void WriteUnPackInfo(BinaryWriter bw, Folder[] Folders)
		{
			bw.Write((byte)HeaderProperty.kUnPackInfo);

			bw.Write((byte)HeaderProperty.kFolder);
			ulong numFolders = (ulong)Folders.Length;
			bw.WriteEncodedUInt64(numFolders);
			bw.Write((byte)0); //External Flag
			for (ulong i = 0; i < numFolders; i++)
				Folders[i].WriteFolder(bw);


			bw.Write((byte)HeaderProperty.kCodersUnPackSize);
			for (ulong i = 0; i < numFolders; i++)
				Folders[i].WriteUnpackedStreamSize(bw);

			if (Folders[0].UnpackCRC != null)
			{
				bw.Write((byte)HeaderProperty.kCRC);
				throw new NotImplementedException();
			}
			bw.Write((byte)HeaderProperty.kEnd);
		}

		public static void WriteSubStreamsInfo(BinaryWriter bw, Folder[] Folders)
		{
			bw.Write((byte)HeaderProperty.kSubStreamsInfo);

			bw.Write((byte)HeaderProperty.kNumUnPackStream);
			for (int f = 0; f < Folders.Length; f++)
			{
				ulong numStreams = (ulong)Folders[f].UnpackedStreamInfo.Length;
				bw.WriteEncodedUInt64(numStreams);
			}

			bw.Write((byte)HeaderProperty.kSize);

			for (int f = 0; f < Folders.Length; f++)
			{
				Folder folder = Folders[f];
				for (int i = 0; i < folder.UnpackedStreamInfo.Length - 1; i++)
				{
					bw.WriteEncodedUInt64(folder.UnpackedStreamInfo[i].UnpackedSize);
				}
			}

			bw.Write((byte)HeaderProperty.kCRC);
			bw.Write((byte)1); // crc flags default to true
			for (int f = 0; f < Folders.Length; f++)
			{
				Folder folder = Folders[f];
				for (int i = 0; i < folder.UnpackedStreamInfo.Length; i++)
				{
					bw.Write(Util.uinttobytes(folder.UnpackedStreamInfo[i].Crc));
				}
			}
			bw.Write((byte)HeaderProperty.kEnd);
		}
	}
}
