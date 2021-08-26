﻿using System.Collections;
using System.Collections.Generic;
using DirectCanvas.Core;
namespace DirectCanvas.Undo
{
    public class CMD_RecoverLayout : IUndoCommand, ICanDeleteCommand
    {
        public readonly PictureLayout layout;
        readonly int insertIndex;
        readonly CanvasCase canvasCase;

        public CMD_RecoverLayout(PictureLayout layout, CanvasCase canvasCase, int insertIndex)
        {
            this.layout = layout;
            this.insertIndex = insertIndex;
            this.canvasCase = canvasCase;
        }
        public void Delete()
        {
            layout.Dispose();
            canvasCase.LayoutTex.Remove(layout.guid, out TiledTexture tiledTexture);
            tiledTexture?.Dispose();
        }

        public void Dispose()
        {
            return;
        }

        public IUndoCommand Execute()
        {
            //canvasCase.watched = false;
            canvasCase.Layouts.Insert(insertIndex, layout);
            //canvasCase.watched = true;
            return new CMD_DeleteLayout(layout, canvasCase, insertIndex);
        }
    }
}