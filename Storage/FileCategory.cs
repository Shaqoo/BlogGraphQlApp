namespace BlogGraphQlApp.Storage
{
    /// <summary>Rough category of an uploaded file, used to pick validation limits.</summary>
    public enum FileCategory
    {
        Image,
        Video,
        Audio,
        Document
    }
}
