﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using Vortice.Direct3D11;

namespace CanvasRendering
{
    public class GraphicsContext
    {
        public GraphicsContext()
        {
            var blendDesc = new BlendDescription
            {
                AlphaToCoverageEnable = false
            };

            blendDesc.RenderTarget[0] = new RenderTargetBlendDescription
            {
                IsBlendEnabled = true,
                SourceBlend = Blend.SourceAlpha,
                DestinationBlend = Blend.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = Blend.InverseSourceAlpha,
                DestinationBlendAlpha = Blend.Zero,
                BlendOperationAlpha = BlendOperation.Add,
                RenderTargetWriteMask = ColorWriteEnable.All
            };
            defaultBlendState = DeviceResources.device.CreateBlendState(blendDesc);

            var stencilOpDesc = new DepthStencilOperationDescription(StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep, ComparisonFunction.Always);
            var depthDesc = new DepthStencilDescription
            {
                DepthEnable = false,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.Always,
                StencilEnable = false,
                FrontFace = stencilOpDesc,
                BackFace = stencilOpDesc
            };

            depthStencilState = DeviceResources.device.CreateDepthStencilState(depthDesc);
        }
        public void ClearScreen()
        {
            var deviceContext = DeviceResources.d3dContext;
            deviceContext.RSSetViewport(0, 0, DeviceResources.m_d3dRenderTargetSize.X, DeviceResources.m_d3dRenderTargetSize.Y);
            deviceContext.OMSetRenderTargets(DeviceResources.renderTargetView1, DeviceResources.depthStencilView);
            deviceContext.ClearRenderTargetView(DeviceResources.renderTargetView1, color);
            deviceContext.ClearDepthStencilView(DeviceResources.depthStencilView, DepthStencilClearFlags.Depth, 1.0f, 0);
        }
        public void SetLogicSize(Vector2 size)
        {
            DeviceResources.SetLogicalSize(size);
        }
        public void SetVertexShader(VertexShader vertexShader)
        {
            VertexShader = vertexShader;
            DeviceResources.d3dContext.VSSetShader(vertexShader.vertexShader);
        }
        public void SetPixelShader(PixelShader pixelShader)
        {
            PixelShader = pixelShader;
            DeviceResources.d3dContext.PSSetShader(pixelShader.pixelShader);
        }
        public void SetCBV(ConstantBuffer constantBuffer, int slot, int ofs, int size)
        {
            int[] ofs1 = { ofs / 16 };
            int[] size1 = { size / 16 };
            DeviceResources.d3dContext.VSSetConstantBuffer1(slot, constantBuffer.m_buffer, ofs1, size1);
            DeviceResources.d3dContext.PSSetConstantBuffer1(slot, constantBuffer.m_buffer, ofs1, size1);
        }
        public void SetCBV(ConstantBuffer constantBuffer, int slot)
        {
            DeviceResources.d3dContext.VSSetConstantBuffer(slot, constantBuffer.m_buffer);
            DeviceResources.d3dContext.PSSetConstantBuffer(slot, constantBuffer.m_buffer);
        }
        public void SetMesh(Mesh mesh)
        {
            var deviceContext = DeviceResources.d3dContext;
            deviceContext.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
            deviceContext.IASetVertexBuffers(0, new VertexBufferView(mesh.vertexBuffer, mesh.stride));
            deviceContext.IASetIndexBuffer(mesh.indexBuffer, Vortice.DXGI.Format.R16_UInt, 0);
            InputLayoutName = mesh.inputLayout;
        }
        public void Present()
        {
            DeviceResources.Present();
        }
        public void SetSRV(RenderTexture texture, int slot)
        {
            DeviceResources.d3dContext.PSSetShaderResource(slot, texture.srv);
        }
        public void RSSetScissorRect(Vortice.RawRect rect)
        {
            DeviceResources.d3dContext.RSSetScissorRect(rect);
        }
        public void SetScissorRectDefault()
        {
            DeviceResources.d3dContext.RSSetScissorRect(new Vortice.RawRect(0, 0, (int)DeviceResources.m_d3dRenderTargetSize.X, (int)DeviceResources.m_d3dRenderTargetSize.Y));
        }
        public void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            var context = DeviceResources.d3dContext;
            context.OMSetBlendState(defaultBlendState);
            context.OMSetDepthStencilState(depthStencilState);
            if (samplerState == null)
            {
                samplerState = DeviceResources.device.CreateSamplerState(new SamplerDescription(Filter.MinMagMipLinear, TextureAddressMode.Wrap, TextureAddressMode.Wrap, TextureAddressMode.Wrap));
            }
            if (rasterizerState == null)
            {
                rasterizerState = DeviceResources.device.CreateRasterizerState(new RasterizerDescription(CullMode.None, FillMode.Solid) { ScissorEnable = true });
            }
            context.PSSetSampler(0, samplerState);
            context.RSSetState(rasterizerState);
            context.IASetInputLayout(VertexShader.GetInputLayout(DeviceResources, InputLayoutName));

            context.DrawIndexed(indexCount, startIndexLocation, baseVertexLocation);
        }
        public void SetSwapChainPanel(object panel, Vector2 compositionScale, Vector2 logicalSize, float dpi) => DeviceResources.SetSwapChainPanel(panel, compositionScale, logicalSize, dpi);
        public void SetClearColor(Vector4 color) => this.color = new Vortice.Mathematics.Color4(color);
        public Vortice.Mathematics.Color4 color;
        public DeviceResources DeviceResources { get; private set; } = new DeviceResources();
        public ID3D11BlendState defaultBlendState;
        public ID3D11SamplerState samplerState;
        public string InputLayoutName;
        ID3D11RasterizerState rasterizerState;

        public VertexShader VertexShader;
        public PixelShader PixelShader;

        ID3D11DepthStencilState depthStencilState;
    }
}
