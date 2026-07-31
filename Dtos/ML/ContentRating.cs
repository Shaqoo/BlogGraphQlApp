using Microsoft.ML.Data;

namespace BlogGraphQlApp.ML
{
    public class ContentRating
    {
        [LoadColumn(0)] public float UserId { get; set; }
        [LoadColumn(1)] public float ContentId { get; set; }
        [LoadColumn(2)] public float Label { get; set; }
    }
}