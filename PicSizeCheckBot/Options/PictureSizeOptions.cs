namespace TehGM.WolfBots.PicSizeCheckBot.Options
{
    public class PictureSizeOptions
    {
        public int MinimumValidSize { get; set; } = 640;
        public int MaximumValidSize { get; set; } = 1600;

        public string UrlMatchingPattern { get; set; } = "^(http:\\/\\/www\\.|https:\\/\\/www\\.|http:\\/\\/|https:\\/\\/|www\\.)[a-z0-9]+([\\-\\.]{1}[a-z0-9]+)*\\.[a-z]{2,5}(:[0-9]{1,5})?(\\/\\S*)?$";
    }
}
