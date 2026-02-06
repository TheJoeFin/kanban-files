namespace KanbanFiles.Services;

public interface IRecentFoldersService
{
    Task<List<string>> GetRecentFoldersAsync();
    Task AddRecentFolderAsync(string folderPath);
    Task RemoveRecentFolderAsync(string folderPath);
}
