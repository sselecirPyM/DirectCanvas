﻿using System;
using System.Collections;
using System.Collections.Generic;
using CanvasRendering;
using System.Numerics;
using NekoPainter.Nodes;

namespace NekoPainter.Core
{
    public class PictureLayout : IDisposable
    {
        public Guid guid;

        public PictureLayout() { }
        public bool Hidden;
        /// <summary>
        /// 图层的名称，用来标识图层。
        /// </summary>
        public string Name;

        /// <summary>
        /// 图层的Alpha值
        /// </summary>
        public float Alpha = 1.0f;

        /// <summary>
        /// 图层的混合模式
        /// </summary>
        public Guid BlendMode { get; set; }

        public Dictionary<string, ParameterN> parameters = new Dictionary<string, ParameterN>();

        public Vector4 Color = Vector4.One;

        public string DataPath = "";

        public PictureLayout(PictureLayout pictureLayout)
        {
            BlendMode = pictureLayout.BlendMode;
            Alpha = pictureLayout.Alpha;
            Color = pictureLayout.Color;
            DataSource = pictureLayout.DataSource;
            graph = pictureLayout.graph.Clone();

            guid = Guid.NewGuid();
        }

        public void Dispose()
        {

        }

        public Graph graph;

        public LayoutDataSource DataSource;

        public bool saved = false;

        public bool generatePicture = false;
    }

    public enum LayoutDataSource
    {
        Default,
        Color,
    }
}
