namespace StudentPropertyMarketplace;

public interface IFileStorage
{
    void SaveFile(string filePath, byte[] content);
}