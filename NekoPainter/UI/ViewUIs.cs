﻿using NekoPainter.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CanvasRendering;
using ImGuiNET;
using System.Numerics;
using Vortice.Direct3D11;
using Vortice.DXGI;
using NekoPainter.Util;

namespace NekoPainter.UI
{
    public static class ViewUIs
    {
        public static bool Initialized = false;
        public static RenderTexture FontAtlas;
        public static ConstantBuffer constantBuffer;
        public static Mesh mesh;
        public static int selectedIndex = -1;
        //public static long TimeCost;
        public static void InputProcess()
        {
            var io = ImGui.GetIO();
            if (ImGui.GetCurrentContext() == default(IntPtr)) return;
            Vector2 mouseMoveDelta = new Vector2();
            float mouseWheelDelta = 0.0f;

            while (Input.inputDatas.TryDequeue(out InputData inputData))
            {
                if (inputData.inputType == InputType.MouseMove)
                    io.MousePos = inputData.point;
                else if (inputData.inputType == InputType.MouseLeftDown)
                {
                    io.MouseDown[0] = inputData.mouseDown;
                    io.MousePos = inputData.point;
                }
                else if (inputData.inputType == InputType.MouseRightDown)
                {
                    io.MouseDown[1] = inputData.mouseDown;
                    io.MousePos = inputData.point;
                }
                else if (inputData.inputType == InputType.MouseMiddleDown)
                {
                    io.MouseDown[2] = inputData.mouseDown;
                }
                else if (inputData.inputType == InputType.MouseWheelChanged)
                {
                    io.MouseWheel += inputData.mouseWheelDelta / 120.0f;
                    mouseWheelDelta += inputData.mouseWheelDelta;
                }
                else if (inputData.inputType == InputType.MouseMoveDelta)
                    mouseMoveDelta += inputData.point;
            }
            Input.deltaWheel = mouseWheelDelta;
        }
        public static void Draw()
        {
            var context = AppController.Instance.graphicsContext;
            var device = context.DeviceResources;
            if (!Initialized)
            {
                Initialize();
            }
            var io = ImGui.GetIO();
            io.DisplaySize = device.m_d3dRenderTargetSize;
            var document = AppController.Instance?.CurrentLivedDocument;

            ImGui.NewFrame();
            ImGui.ShowDemoWindow();
            CreateDocument();
            OpenDocument();
            MainMenuBar();

            while (Input.penInputData.TryDequeue(out var result))
            {
                Input.penInputData1.Enqueue(result);
            }
            //Input.penInputData.Clear();
            if (document != null)
            {
                LayoutsPanel();

                ImGui.SetNextWindowSize(new Vector2(200, 180), ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(new Vector2(200, 20), ImGuiCond.FirstUseEver);
                if (ImGui.Begin("混合模式"))
                {
                    if (document.SelectedLayout != null)
                    {
                        for (int i = 0; i < document.blendModes.Count; i++)
                        {
                            Core.BlendMode blendMode = document.blendModes[i];
                            bool selected = blendMode.Guid == document.SelectedLayout.BlendMode;
                            ImGui.Selectable(string.Format("{0}###{1}", blendMode.Name, blendMode.Guid), ref selected);
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip(blendMode.Description);
                            if (blendMode.Guid != document.SelectedLayout.BlendMode && selected)
                            {
                                document.SetBlendMode(document.SelectedLayout, blendMode);
                            }
                        }
                    }
                }
                ImGui.End();

                LayoutInfoPanel();
                BrushPanel();
                BrushParametersPanel();
                ThumbnailPanel();
                foreach (var livedDocument in AppController.Instance.livedDocuments)
                {
                    Canvas(livedDocument.Value, livedDocument.Key);
                }
            }

            ImGui.Render();
            Input.uiMouseCapture = io.WantCaptureMouse;
            Input.mousePos = io.MousePos;
        }

        static void LayoutsPanel()
        {
            var document = AppController.Instance?.CurrentLivedDocument;
            ImGui.SetNextWindowSize(new Vector2(200, 180), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(0, 20), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("图层"))
            {
                if (ImGui.Button("新建"))
                {
                    if (selectedIndex != -1)
                    {
                        document.NewStandardLayout(selectedIndex);
                    }
                    else if (document != null)
                    {
                        document.NewStandardLayout(0);
                    }
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("新建图层");
                ImGui.SameLine();
                if (ImGui.Button("复制"))
                {
                    if (selectedIndex != -1)
                        document.CopyLayout(selectedIndex);
                }
                ImGui.SameLine();
                if (ImGui.Button("删除"))
                {
                    if (selectedIndex != -1)
                        document.DeleteLayout(selectedIndex);
                }

                if (document != null)
                {
                    selectedIndex = -1;
                    var layouts = document.Layouts;
                    for (int i = 0; i < layouts.Count; i++)
                    {
                        var layout = layouts[i];

                        bool selected = layout == document.SelectedLayout;
                        //if (ImGui.Button(string.Format("{0}###0{1}", layout.Hidden ? "显示" : "隐藏", layout.guid)))
                        //    layout.Hidden = !layout.Hidden;
                        //ImGui.SameLine();
                        ImGui.Selectable(string.Format("{0}###1{1}", layout.Name, layout.guid), ref selected);
                        if (selected)
                        {
                            if (layout != document.SelectedLayout)
                                document.SetActivatedLayout(layout);
                            document.SelectedLayout = layout;
                            selectedIndex = i;
                        }
                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                        {
                            int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                            if (n_next >= 0 && n_next < layouts.Count)
                            {
                                layouts[i] = layouts[n_next];
                                layouts[n_next] = layout;
                                ImGui.ResetMouseDragDelta();
                                document.UndoManager.AddUndoData(new Undo.CMD_MoveLayout(document, i, n_next));
                            }
                        }
                    }
                }
                ImGui.EndChildFrame();
            }
            ImGui.End();
        }

        static void LayoutInfoPanel()
        {
            var document = AppController.Instance?.CurrentLivedDocument;
            ImGui.SetNextWindowSize(new Vector2(200, 200), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(200, 200), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("图层信息") && document.SelectedLayout != null)
            {
                var layout = document.SelectedLayout;
                ImGui.SliderFloat("Alpha", ref layout.Alpha, 0, 1);
                ImGui.ColorEdit4("颜色", ref layout.Color);
                bool useColor = layout.DataSource == Core.PictureDataSource.Color;
                ImGui.Checkbox("使用颜色", ref useColor);
                layout.DataSource = useColor ? Core.PictureDataSource.Color : Core.PictureDataSource.Default;
                ImGui.Checkbox("隐藏", ref layout.Hidden);

                if (document.blendmodesMap.TryGetValue(layout.BlendMode, out var blendMode) && blendMode.Paramerters != null)
                {
                    for (int i = 0; i < blendMode.Paramerters.Length; i++)
                    {
                        layout.parameters.TryGetValue(blendMode.Paramerters[i].Name, out var parameter);
                        float f1 = (float)parameter.X;
                        parameter.Name = blendMode.Paramerters[i].Name;
                        if (ImGui.DragFloat(string.Format("{0}###{1}", blendMode.Paramerters[i].Name, i), ref f1))
                        {
                            parameter.X = f1;
                            layout.parameters[blendMode.Paramerters[i].Name] = parameter;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(blendMode.Paramerters[i].Description);
                        }
                    }
                }
            }
            ImGui.End();
        }

        static void ThumbnailPanel()
        {
            var io = ImGui.GetIO();
            ImGui.SetNextWindowSize(new Vector2(200, 200), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(200, 600), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("缩略图"))
            {
                string texPath = string.Format("{0}/Canvas", AppController.Instance.CurrentLivedDocument.Path);
                IntPtr imageId = new IntPtr(AppController.Instance.GetId(texPath));
                Vector2 pos = ImGui.GetCursorScreenPos();
                var tex = AppController.Instance.GetTexture(texPath);
                Vector2 spaceSize = ImGui.GetWindowSize() - new Vector2(20, 40);
                float factor = MathF.Max(MathF.Min(spaceSize.X / tex.width, spaceSize.Y / tex.height), 0.01f);

                Vector2 imageSize = new Vector2(tex.width * factor, tex.height * factor);

                ImGui.InvisibleButton("X", imageSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
                ImGui.GetWindowDrawList().AddImage(imageId, pos, pos + imageSize);
                if (ImGui.IsItemHovered())
                {
                    Vector2 uv0 = (io.MousePos - pos) / imageSize - new Vector2(100, 100) / new Vector2(tex.width, tex.height);
                    Vector2 uv1 = uv0 + new Vector2(200, 200) / new Vector2(tex.width, tex.height);

                    ImGui.BeginTooltip();
                    ImGui.Image(imageId, new Vector2(100, 100), uv0, uv1);
                    ImGui.EndTooltip();
                }
            }
            ImGui.End();
        }


        static PenInputFlag currentState;
        static void Canvas(LivedNekoPainterDocument document, string path)
        {
            var io = ImGui.GetIO();
            var paintAgent = AppController.Instance?.CurrentLivedDocument?.PaintAgent;
            ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(400, 0), ImGuiCond.FirstUseEver);
            if (ImGui.Begin(string.Format("画布{0}###{1}", document.Name, path)))
            {
                if (ImGui.IsWindowFocused())
                {
                    AppController.Instance.CurrentDCDocument = AppController.Instance.documents[path];
                    AppController.Instance.CurrentLivedDocument = AppController.Instance.livedDocuments[path];
                }
                string texPath = string.Format("{0}/Canvas", path);
                IntPtr imageId = new IntPtr(AppController.Instance.GetId(texPath));
                Vector2 pos = ImGui.GetCursorScreenPos();
                var tex = AppController.Instance.GetTexture(texPath);

                Vector2 spaceSize = ImGui.GetWindowSize() - new Vector2(20, 40);
                float factor = MathF.Max(MathF.Min(spaceSize.X / document.Width, spaceSize.Y / document.Height), 0.01f);

                Vector2 imageSize = new Vector2(document.Width * factor, document.Height * factor);

                ImGui.InvisibleButton("X", imageSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
                ImGui.GetWindowDrawList().AddImage(imageId, pos, pos + imageSize);

                if (ImGui.IsItemActive() || (currentState == PenInputFlag.Drawing && ImGui.IsWindowFocused()))
                {
                    while (Input.penInputData1.TryDequeue(out var penInput))
                    {
                        if (!(currentState == PenInputFlag.End && penInput.penInputFlag == PenInputFlag.Drawing))
                        {
                            currentState = penInput.penInputFlag;
                            penInput.point = (penInput.point - pos) / factor;
                            switch (penInput.penInputFlag)
                            {
                                case PenInputFlag.Begin:
                                    paintAgent.DrawBegin(penInput);
                                    break;
                                case PenInputFlag.Drawing:
                                    paintAgent.Draw(penInput);
                                    break;
                                case PenInputFlag.End:
                                    paintAgent.DrawEnd(penInput);
                                    break;
                            }
                        }
                    }
                }

                //if (ImGui.IsItemHovered())
                //{
                //    Vector2 uv0 = (io.MousePos - pos) / imageSize - new Vector2(100, 100) / new Vector2(tex.width, tex.height);
                //    Vector2 uv1 = uv0 + new Vector2(200, 200) / new Vector2(tex.width, tex.height);

                //    ImGui.BeginTooltip();
                //    ImGui.Image(imageId, new Vector2(100, 100), uv0, uv1);
                //    ImGui.EndTooltip();
                //}
            }
            ImGui.End();
        }

        static void BrushParametersPanel()
        {
            var paintAgent = AppController.Instance?.CurrentLivedDocument?.PaintAgent;
            ImGui.SetNextWindowSize(new Vector2(200, 200), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(0, 200), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("笔刷参数"))
            {
                //ImGui.Text(TimeCost.ToString());
                ImGui.SliderFloat("笔刷尺寸", ref paintAgent.BrushSize, 1, 300);
                ImGui.ColorEdit4("颜色", ref paintAgent._color);
                ImGui.ColorEdit4("颜色2", ref paintAgent._color2);
                ImGui.ColorEdit4("颜色3", ref paintAgent._color3);
                ImGui.ColorEdit4("颜色4", ref paintAgent._color4);
                if (paintAgent.currentBrush != null && paintAgent.currentBrush.Parameters != null)
                {
                    var brushParams = paintAgent.currentBrush.Parameters;
                    for (int i = 0; i < brushParams.Length; i++)
                    {
                        float a1 = (float)brushParams[i].Value;
                        ImGui.DragFloat(string.Format("{0}###{1}", brushParams[i].Name, i), ref a1);
                        brushParams[i].Value = a1;
                    }
                }
            }
            ImGui.End();
        }

        static void BrushPanel()
        {
            var paintAgent = AppController.Instance?.CurrentLivedDocument?.PaintAgent;

            ImGui.SetNextWindowSize(new Vector2(200, 200), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(0, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("笔刷"))
            {
                var brushes = paintAgent.brushes;
                for (int i = 0; i < brushes.Count; i++)
                {
                    Core.Brush brush = brushes[i];
                    bool selected = brush == paintAgent.currentBrush;
                    ImGui.Selectable(brush.Name, ref selected);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(brush.Description);
                    if (selected)
                    {
                        paintAgent.SetBrush(brush);
                    }
                    if (ImGui.IsItemActive() && !ImGui.IsItemHovered())
                    {
                        int n_next = i + (ImGui.GetMouseDragDelta(0).Y < 0.0f ? -1 : 1);
                        if (n_next >= 0 && n_next < brushes.Count)
                        {
                            brushes[i] = brushes[n_next];
                            brushes[n_next] = brush;
                            ImGui.ResetMouseDragDelta();
                        }
                    }
                }
            }

            ImGui.End();
        }

        static void MainMenuBar()
        {
            var document = AppController.Instance?.CurrentLivedDocument;
            bool canUndo = false;
            bool canRedo = false;
            if (document?.UndoManager.UndoStackIsNotEmpty == true)
                canUndo = true;
            if (document?.UndoManager.RedoStackIsNotEmpty == true)
                canRedo = true;

            var io = ImGui.GetIO();
            ImGui.BeginMainMenuBar();
            bool beginMenuFile = ImGui.BeginMenu("File");
            if (beginMenuFile)
            {
                if (ImGui.MenuItem("New"))
                {
                    newDocument = true;
                }
                if (ImGui.MenuItem("Open", "CTRL+O"))
                {
                    openDocument = true;
                }
                if (ImGui.MenuItem("Save", "CTRL+S"))
                {
                    UIHelper.saveDocument = true;
                }
                ImGui.Separator();
                if (ImGui.MenuItem("Import"))
                {
                    UIHelper.selectOpenFile = true;
                }
                ImGui.MenuItem("Export");
                ImGui.Separator();
                ImGui.MenuItem("Exit");
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Undo", "CTRL+Z", false, canUndo))
                {
                    document.UndoManager.Undo();
                }
                if (ImGui.MenuItem("Redo", "CTRL+Y", false, canRedo))
                {
                    document.UndoManager.Redo();
                }

                ImGui.Separator();
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("View"))
            {
                ImGui.EndMenu();
            }
            if (document != null)
            {
                ImGui.Text(document.Name);
            }
            else
            {
                ImGui.Text("No document");
            }
            ImGui.EndMainMenuBar();
            if (document != null)
            {
                if (canUndo && io.KeyCtrl && ImGui.IsKeyPressed('Z'))
                {
                    document.UndoManager.Undo();
                }
                if (canRedo && io.KeyCtrl && ImGui.IsKeyPressed('Y'))
                {
                    document.UndoManager.Redo();
                }
                if (io.KeyCtrl && ImGui.IsKeyPressed('S'))
                {
                    UIHelper.saveDocument = true;
                }
            }
        }

        static void OpenDocument()
        {
            if (openDocument.SetFalse())
            {
                ImGui.OpenPopup("OpenDocument");
            }
            ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.FirstUseEver);
            if (ImGui.BeginPopupModal("OpenDocument"))
            {
                if (UIHelper.folder != null)
                {
                    CreateDocumentParameters.Folder = UIHelper.folder.FullName;
                    UIHelper.folder = null;
                }
                ImGui.SetNextItemWidth(200);
                ImGui.InputText("path", ref UIHelper.openDocumentPath, 260);

                ImGui.SameLine();
                if (ImGui.Button("浏览"))
                {
                    UIHelper.selectFolder = true;
                }
                if (ImGui.Button("打开"))
                {
                    if (!string.IsNullOrEmpty(UIHelper.openDocumentPath))
                    {
                        ImGui.CloseCurrentPopup();
                        UIHelper.openDocument = true;
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        static void CreateDocument()
        {
            if (newDocument.SetFalse())
            {
                ImGui.OpenPopup("NewDocument");
                CreateDocumentParameters.Name = "NewDocument";
            }
            ImGui.SetNextWindowSize(new Vector2(400, 400), ImGuiCond.FirstUseEver);
            if (ImGui.BeginPopupModal("NewDocument"))
            {
                if (UIHelper.folder != null)
                {
                    CreateDocumentParameters.Folder = UIHelper.folder.FullName;
                    UIHelper.folder = null;
                }
                ImGui.SetNextItemWidth(200);
                if (CreateDocumentParameters.Folder != null)
                    ImGui.InputText("path", ref CreateDocumentParameters.Folder, 260);
                else
                    ImGui.InputText("path", ref CreateDocumentParameters.Folder, 260);

                ImGui.SameLine();
                if (ImGui.Button("浏览"))
                {
                    UIHelper.selectFolder = true;
                }
                ImGui.InputText("文档名", ref CreateDocumentParameters.Name, 200);
                ImGui.InputInt("宽度", ref CreateDocumentParameters.Width);
                ImGui.InputInt("高度", ref CreateDocumentParameters.Height);
                if (ImGui.Button("创建"))
                {
                    ImGui.CloseCurrentPopup();
                    UIHelper.createDocumentParameters = CreateDocumentParameters;
                    UIHelper.createDocument = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("取消"))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        public static void Render()
        {
            var data = ImGui.GetDrawData();
            var context = AppController.Instance.graphicsContext;
            var appcontroller = AppController.Instance;
            float L = data.DisplayPos.X;
            float R = data.DisplayPos.X + data.DisplaySize.X;
            float T = data.DisplayPos.Y;
            float B = data.DisplayPos.Y + data.DisplaySize.Y;
            float[] mvp =
            {
                    2.0f/(R-L),   0.0f,           0.0f,       0.0f,
                    0.0f,         2.0f/(T-B),     0.0f,       0.0f,
                    0.0f,         0.0f,           0.5f,       0.0f,
                    (R+L)/(L-R),  (T+B)/(B-T),    0.5f,       1.0f,
            };
            constantBuffer.UpdateResource(new Span<float>(mvp));
            var vertexShader = AppController.Instance.vertexShaders["VSImgui"];
            var pixelShader = AppController.Instance.pixelShaders["PSImgui"];
            context.SetVertexShader(vertexShader);
            context.SetPixelShader(pixelShader);
            //context.SetCBV(constantBuffer, 0);
            context.SetCBV(constantBuffer, 0, 0, 256);
            Vector2 clip_off = data.DisplayPos;
            byte[] vertexDatas;
            byte[] indexDatas;
            unsafe
            {
                vertexDatas = new byte[data.TotalVtxCount * sizeof(ImDrawVert)];
                indexDatas = new byte[data.TotalIdxCount * sizeof(UInt16)];
                int vtxByteOfs = 0;
                int idxByteOfs = 0;
                for (int i = 0; i < data.CmdListsCount; i++)
                {
                    var cmdList = data.CmdListsRange[i];
                    var vertBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
                    var indexBytes = cmdList.IdxBuffer.Size * sizeof(UInt16);
                    new Span<byte>(cmdList.VtxBuffer.Data.ToPointer(), vertBytes).CopyTo(new Span<byte>(vertexDatas, vtxByteOfs, vertBytes));
                    new Span<byte>(cmdList.IdxBuffer.Data.ToPointer(), indexBytes).CopyTo(new Span<byte>(indexDatas, idxByteOfs, indexBytes));
                    vtxByteOfs += vertBytes;
                    idxByteOfs += indexBytes;
                }
                mesh.Update(vertexDatas, indexDatas);
                //System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                //stopwatch.Start();
                //stopwatch.Stop();
                //TimeCost = stopwatch.ElapsedTicks;
                int vtxOfs = 0;
                int idxOfs = 0;
                for (int i = 0; i < data.CmdListsCount; i++)
                {
                    var cmdList = data.CmdListsRange[i];
                    context.SetMesh(mesh);
                    for (int j = 0; j < cmdList.CmdBuffer.Size; j++)
                    {
                        var cmd = cmdList.CmdBuffer[j];
                        var rect = new Vortice.RawRect((int)(cmd.ClipRect.X - clip_off.X), (int)(cmd.ClipRect.Y - clip_off.Y), (int)(cmd.ClipRect.Z - clip_off.X), (int)(cmd.ClipRect.W - clip_off.Y));
                        context.RSSetScissorRect(rect);
                        context.SetSRV(appcontroller.textures[(long)cmd.TextureId], 0);
                        context.DrawIndexed((int)cmd.ElemCount, (int)cmd.IdxOffset + idxOfs, (int)cmd.VtxOffset + vtxOfs);
                    }
                    vtxOfs += cmdList.VtxBuffer.Size;
                    idxOfs += cmdList.IdxBuffer.Size;
                }
            }
            context.SetScissorRectDefault();
        }

        public static void Initialize()
        {
            Initialized = true;
            var appcontroller = AppController.Instance;
            var imguiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(imguiContext);
            var io = ImGui.GetIO();
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            var device = AppController.Instance.graphicsContext.DeviceResources;
            constantBuffer = new ConstantBuffer(device, 64);
            mesh = new Mesh(device, 20, unnamedInputLayout);
            io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 13, null, io.Fonts.GetGlyphRangesChineseFull());
            FontAtlas = new RenderTexture();
            unsafe
            {
                io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height);
                int size = width * height * 4;
                byte[] data = new byte[size];
                Span<byte> _pixels = new Span<byte>(pixels, size);
                _pixels.CopyTo(data);

                FontAtlas.Create2(device, width, height, Vortice.DXGI.Format.R8G8B8A8_UNorm, false, data);
            }
            io.Fonts.TexID = new IntPtr(appcontroller.GetId("ImguiFont"));
            appcontroller.AddTexture("ImguiFont", FontAtlas);
        }

        public static CreateDocumentParameters CreateDocumentParameters = new CreateDocumentParameters();

        static bool newDocument;
        static bool openDocument;
        public static UnnamedInputLayout unnamedInputLayout = new UnnamedInputLayout
        {
            inputElementDescriptions = new InputElementDescription[]
                {
                    new InputElementDescription("POSITION",0,Format.R32G32_Float,0),
                    new InputElementDescription("TEXCOORD",0,Format.R32G32_Float,0),
                    new InputElementDescription("COLOR",0,Format.R8G8B8A8_UNorm,0),
                }
        };
    }
}
