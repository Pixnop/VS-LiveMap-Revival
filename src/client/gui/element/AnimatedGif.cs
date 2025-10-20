using SkiaSharp;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace livemap.client.gui;

public class AnimatedGif : GuiElement {
    private readonly Frame[] _frames;

    private LoadedTexture _texture;

    private long _lastUpdateTime;
    private int _currentFrame;

    public AnimatedGif(ICoreClientAPI api, byte[] gifData) : base(api, ElementBounds.Fill) {
        _texture = new LoadedTexture(api);

        using MemoryStream stream = new(gifData);

        SKCodec codec = SKCodec.Create(stream);
        SKImageInfo info = new(codec.Info.Width, codec.Info.Height);

        _frames = new Frame[codec.FrameCount];

        for (int i = 0; i < _frames.Length; i++) {
            BitmapExternal bitmap = new(new SKBitmap(info));

            codec.GetPixels(info, bitmap.PixelsPtrAndLock, new SKCodecOptions(i));
            codec.GetFrameInfo(i, out SKCodecFrameInfo frameInfo);

            _frames[i] = new Frame(bitmap, frameInfo.Duration);
        }
    }

    public override void RenderInteractiveElements(float deltaTime) {
        Bounds.CalcWorldBounds();

        long currentTime = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        if (_lastUpdateTime + _frames[_currentFrame].Duration <= currentTime) {
            _lastUpdateTime = currentTime;
            _currentFrame = (_currentFrame + 1) % _frames.Length;

            api.Render.LoadTexture(_frames[_currentFrame].Bitmap, ref _texture);
        }

        api.Render.Render2DTexture(
            _texture.TextureId,
            (float)Bounds.renderX,
            (float)Bounds.renderY,
            (float)Bounds.InnerWidth,
            (float)Bounds.InnerHeight
        );
    }

    private class Frame(BitmapRef bitmap, int duration) {
        public BitmapRef Bitmap { get; } = bitmap;
        public int Duration { get; } = duration;
    }
}
