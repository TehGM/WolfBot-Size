using System;

namespace TehGM.WolfBots.PicSizeCheckBot.SizeChecking
{
    public struct PictureSize
    {
        public readonly int Width;
        public readonly int Height;

        public readonly int MaxSize;
        public readonly int MinSize;

        public bool IsTooBig => this.Width > this.MaxSize || this.Height > this.MaxSize;
        public bool IsTooSmall => this.Width < this.MinSize || this.Height < this.MinSize;
        public bool IsSquare => this.Width == this.Height;

        public bool IsValid => this.IsSquare && !this.IsTooBig && this.IsTooSmall;

        public override string ToString()
            => $"{this.Width} x {this.Height}";

        public PictureSize(int picWidth, int picHeight, int minSize, int maxSize)
        {
            if (picWidth < 0)
                throw new ArgumentException("Picture dimensions cannot be negative", nameof(picWidth));
            if (picHeight < 0)
                throw new ArgumentException("Picture dimensions cannot be negative", nameof(picHeight));
            if (minSize < 0)
                throw new ArgumentException("Size restrictions cannot be negative", nameof(minSize));
            if (maxSize < 0)
                throw new ArgumentException("Size restrictions cannot be negative", nameof(maxSize));
            if (maxSize < minSize)
                throw new ArgumentException("Max size cannot be less than min size", nameof(maxSize));

            this.Width = picWidth;
            this.Height = picHeight;
            this.MinSize = minSize;
            this.MaxSize = maxSize;
        }

    }
}
