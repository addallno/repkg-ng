using System.IO;

namespace RePKG.Core.Texture
{
    public interface ITexReader
    {
        ITex ReadFrom(BinaryReader reader, PackageFormat packageFormat = PackageFormat.V);
    }
}