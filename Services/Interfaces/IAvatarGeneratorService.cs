using BlogGraphQlApp.Services.Implementations;

namespace BlogGraphQlApp.Services.Interfaces
{
    public interface IAvatarGeneratorService
    {
        byte[] GenerateAvatar(string initials, int size = 200, bool useGradient = true, bool addBorderRing = true, AvatarShape shape = AvatarShape.Circle,int borderThickness = 6, string fontFamily = "Arial");
    }
}
