﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using NekoPainter.Data;
using Newtonsoft.Json;

namespace NekoPainter.Core.Util
{
    public static class StringConvert
    {
        static JsonConverter[] converters = new[] { new VectorConverter() };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetFloat(string input)
        {
            float.TryParse(input, out float f);
            return f;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetFloat2(string input)
        {
            if (input == null) return new Vector2();
            return JsonConvert.DeserializeObject<Vector2>(input, converters);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 GetFloat3(string input)
        {
            if (input == null) return new Vector3();
            return JsonConvert.DeserializeObject<Vector3>(input, converters);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 GetFloat4(string input)
        {
            if (input == null) return new Vector4();
            return JsonConvert.DeserializeObject<Vector4>(input, converters);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInt(string input)
        {
            if (input == null) return 0;
            return int.Parse(input);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int2 GetInt2(string input)
        {
            if (input == null) return new Int2();
            return JsonConvert.DeserializeObject<Int2>(input, converters);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int3 GetInt3(string input)
        {
            if (input == null) return new Int3();
            return JsonConvert.DeserializeObject<Int3>(input, converters);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int4 GetInt4(string input)
        {
            if (input == null) return new Int4();
            return JsonConvert.DeserializeObject<Int4>(input, converters);
        }
    }
}
