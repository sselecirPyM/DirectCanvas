﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using CanvasRendering;
using NekoPainter.Data;

namespace NekoPainter
{
    /// <summary>
    /// 稀疏纹理
    /// </summary>
    public class TiledTexture : System.IDisposable
    {
        public static ComputeShader Texture2TT;
        public static ComputeShader TextureEmptyTest;
        public static ComputeShader TT2Texture;
        public static ComputeShader TTPartCopy;

        public void Dispose()
        {
            BlocksData?.Dispose();
            BlocksOffsetsData?.Dispose();
            _BlocksData = null;
            _BlocksOffsetsData = null;
        }

        public ComputeBuffer BlocksData;
        public ComputeBuffer BlocksOffsetsData;
        public byte[] _BlocksData;
        public byte[] _BlocksOffsetsData;
        //public DeviceResources deviceResources;

        //public TileIndexCollection TilesStatus;

        public List<Int2> TilePositionList;

        public TiledTexture(RenderTexture tex)
        {
            var deviceResources = tex.GetDevice();
            int width = tex.width;
            int height = tex.height;
            int x = (width + 31) / 32;
            int y = (height + 31) / 32;
            int x2 = (width + 7) / 8;
            int y2 = (height + 7) / 8;
            int tilesmax = x2 * y2;

            int[] oData = new int[tilesmax];
            ComputeBuffer tResult = new ComputeBuffer(deviceResources, tilesmax, 4);
            TextureEmptyTest.SetSRV(tex, 0);
            TextureEmptyTest.SetUAV(tResult, 0);
            TextureEmptyTest.Dispatch(x, y, 1);
            tResult.GetData<int>(oData);
            tResult.Dispose();
            TilePositionList = new List<Int2>();
            for (int i = 0; i < tilesmax; i++)
            {
                if (oData[i] != 0)
                {
                    Int2 a = new Int2((i % x2) * 8, (i / x2) * 8);
                    TilePositionList.Add(a);
                }
            }
            tilesCount = TilePositionList.Count;
            if (tilesCount == 0) return;

            BlocksData = new ComputeBuffer(tex.GetDevice(), tilesCount, 1024);

            BlocksOffsetsData = ComputeBuffer.New<Int2>(tex.GetDevice(), tilesCount, 8, TilePositionList.ToArray());

            Texture2TT.SetSRV(tex, 0);
            Texture2TT.SetSRV(BlocksOffsetsData, 1);
            Texture2TT.SetUAV(BlocksData, 0);
            Texture2TT.Dispatch(1, 1, (tilesCount + 15) / 16);
            tileRect = GetBouding(TilePositionList);
            //TilesStatus = new TileIndexCollection(tileRect, TilePositionList);
        }
        public TiledTexture(RenderTexture tex, List<Int2> tiles)
        {
            var deviceResources = tex.GetDevice();
            tilesCount = tiles.Count;
            BlocksData = new ComputeBuffer(deviceResources, tilesCount, 1024);
            BlocksOffsetsData = ComputeBuffer.New<Int2>(deviceResources, tilesCount, 8, tiles.ToArray());
            Texture2TT.SetSRV(tex, 0);
            Texture2TT.SetSRV(BlocksOffsetsData, 1);
            Texture2TT.SetUAV(BlocksData, 0);
            Texture2TT.Dispatch(1, 1, (tilesCount + 15) / 16);

            TilePositionList = new List<Int2>(tiles);
            tileRect = GetBouding(TilePositionList);
            //TilesStatus = new TileIndexCollection(tileRect, TilePositionList);
        }

        public TiledTexture(TiledTexture tiledTexture)
        {
            tileRect = tiledTexture.tileRect;
            if (tiledTexture.BlocksData == null)
            {
                TilePositionList = new List<Int2>(1);
                //TilesStatus = new TileIndexCollection(new Rectangle());
                tilesCount = 0;
            }
            else
            {
                tilesCount = tiledTexture.tilesCount;
                TilePositionList = new List<Int2>(tiledTexture.TilePositionList);
                //TilesStatus = new TileIndexCollection(tiledTexture.TilesStatus);
                BlocksData = new ComputeBuffer(tiledTexture.BlocksData);
                BlocksOffsetsData = new ComputeBuffer(tiledTexture.BlocksOffsetsData);
                _BlocksData = tiledTexture._BlocksData;
                _BlocksOffsetsData = tiledTexture._BlocksOffsetsData;
            }
        }

        //public TiledTexture(TiledTexture tiledTexture, List<Int2> tiles)
        //{
        //    tilesCount = tiles.Count;
        //    TilePositionList = new List<Int2>(tiles);
        //    List<int> indexs = new List<int>();
        //    if (tilesCount == 0)
        //    {
        //        return;
        //    }

        //    for (int i = 0; i < tilesCount; i++)
        //    {
        //        int tIndex = tiledTexture.TilesStatus[tiles[i]];
        //        if (tIndex != -1)
        //        {
        //            indexs.Add(tIndex);
        //        }
        //        else
        //        {
        //            indexs.Add(magicNumber);
        //        }
        //    }
        //    BlocksOffsetsData = ComputeBuffer.New<Int2>(deviceResources, tilesCount, 8, tiles.ToArray());
        //    BlocksData = new ComputeBuffer(deviceResources, tilesCount, 1024);
        //    if (tiledTexture.BlocksData != null)
        //    {
        //        ComputeBuffer indicates = ComputeBuffer.New<int>(deviceResources, tilesCount, 4, indexs.ToArray());
        //        TTPartCopy.SetSRV(tiledTexture.BlocksData, 0);
        //        TTPartCopy.SetSRV(indicates, 1);
        //        TTPartCopy.SetUAV(BlocksData, 0);

        //        TTPartCopy.Dispatch(1, 1, (tilesCount + 15) / 16);
        //        indicates.Dispose();
        //    }
        //    tileRect = GetBouding(TilePositionList);
        //    TilesStatus = new TileIndexCollection(tileRect, TilePositionList);
        //}

        //private TiledTexture(DeviceResources deviceResources, int _tilesCount)
        //{
        //    this.deviceResources = deviceResources;
        //    tilesCount = _tilesCount;
        //}

        public TiledTexture(byte[] data, byte[] offsetsData)
        {
            tilesCount = offsetsData.Length / 8;
            TilePositionList = new List<Int2>(tilesCount);
            if (tilesCount == 0) return;
            for (int i = 0; i < tilesCount; i++)
            {
                Int2 vector2 = new Int2(System.BitConverter.ToInt32(offsetsData, i * 8), System.BitConverter.ToInt32(offsetsData, i * 8 + 4));
                TilePositionList.Add(vector2);
            }
            _BlocksData = data;
            _BlocksOffsetsData = offsetsData;
            //BlocksData = ComputeBuffer.New<byte>(deviceResources, tilesCount, 1024, data);
            //BlocksOffsetsData = ComputeBuffer.New<byte>(deviceResources, tilesCount, 8, offsetsData);
            tileRect = GetBouding(TilePositionList);
            //TilesStatus = new TileIndexCollection(tileRect, TilePositionList);
        }

        public void UnzipToTexture(RenderTexture tex)
        {
            Check(tex.GetDevice());
            if (BlocksData == null) return;

            TT2Texture.SetSRV(BlocksData, 0);
            TT2Texture.SetSRV(BlocksOffsetsData, 1);
            TT2Texture.SetUAV(tex, 0);
            TT2Texture.Dispatch(1, 1, (tilesCount + 15) / 16);
        }

        public void Check(DeviceResources deviceResources)
        {
            if (_BlocksData != null && BlocksData == null)
            {
                BlocksData = ComputeBuffer.New<byte>(deviceResources, tilesCount, 1024, _BlocksData);
                BlocksOffsetsData = ComputeBuffer.New<byte>(deviceResources, tilesCount, 8, _BlocksOffsetsData);
            }
        }

        public byte[] GetBlocksData()
        {
            if (_BlocksData != null) return _BlocksData;
            byte[] bData = new byte[tilesCount * 1024];
            BlocksData.GetData<byte>(bData);
            return bData;
        }
        public byte[] GetBlocksOffsetsData()
        {
            if (_BlocksOffsetsData != null) return _BlocksOffsetsData;
            byte[] oData = new byte[tilesCount * 8];
            BlocksOffsetsData.GetData<byte>(oData);
            return oData;
        }

        //const int magicNumber = 0x40000000;
        //public static TiledTexture ReplaceTiles(TiledTexture source, TiledTexture target)
        //{
        //    if (source.tilesCount != 0)
        //    {
        //        List<int> indexs = new List<int>();
        //        List<Int2> ofs = new List<Int2>(source.TilePositionList);
        //        for (int i = 0; i < source.tilesCount; i++)
        //        {
        //            indexs.Add(i);
        //        }

        //        for (int i = 0; i < target.tilesCount; i++)
        //        {
        //            if (source.TilesStatus.TryGetValue(target.TilePositionList[i], out int ix))
        //            {
        //                indexs[ix] = i + magicNumber;
        //                ofs[ix] = target.TilePositionList[i];
        //            }
        //            else
        //            {
        //                indexs.Add(i + magicNumber);
        //                ofs.Add(target.TilePositionList[i]);
        //            }
        //        }
        //        TiledTexture ttOut = new TiledTexture(source.deviceResources, indexs.Count);
        //        ttOut.TilePositionList = ofs;
        //        ttOut.tileRect = new TileRect(ofs);
        //        ttOut.TilesStatus = new TileIndexCollection(ttOut.tileRect, ofs);
        //        for (int i = 0; i < ofs.Count; i++)
        //        {
        //            ttOut.TilesStatus.Add(ofs[i], i);
        //        }

        //        ttOut.BlocksData = new ComputeBuffer(source.deviceResources, indexs.Count, 1024);
        //        ttOut.BlocksOffsetsData = new ComputeBuffer(source.deviceResources, indexs.Count, 8, ofs.ToArray());

        //        ComputeBuffer tempIndexs = new ComputeBuffer(source.deviceResources, indexs.Count, 4, indexs.ToArray());
        //        TTReplace.SetSRV(source.BlocksData, 0);
        //        TTReplace.SetSRV(target.BlocksData, 1);
        //        TTReplace.SetSRV(tempIndexs, 2);
        //        TTReplace.SetUAV(ttOut.BlocksData, 0);
        //        TTReplace.Dispatch(1, 1, (ttOut.tilesCount + 15) / 16);
        //        tempIndexs.Dispose();

        //        return ttOut;
        //    }
        //    else
        //        return new TiledTexture(target);
        //}
        Rectangle GetBouding(List<Int2> int2s)
        {
            if (int2s.Count == 0)
                return new Rectangle();
            int minX = int2s[0].X;
            int maxX = int2s[0].X;
            int minY = int2s[0].Y;
            int maxY = int2s[0].Y;
            for (int i = 1; i < int2s.Count; i++)
            {
                minX = Math.Min(minX, int2s[i].X);
                maxX = Math.Max(maxX, int2s[i].X);
                minY = Math.Min(minY, int2s[i].Y);
                maxY = Math.Max(maxY, int2s[i].Y);
            }
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        //public static TiledTexture ReplaceTiles(TiledTexture source, TiledTexture target, RenderTexture tempTexture)
        //{
        //    if (source.tilesCount != 0)
        //    {
        //        tempTexture.Clear();
        //        source.UnzipToTexture(tempTexture);
        //        target.UnzipToTexture(tempTexture);
        //        return new TiledTexture(tempTexture);
        //    }
        //    else return new TiledTexture(target);
        //}
        public Rectangle tileRect;

        public int tilesCount;
    }

}
