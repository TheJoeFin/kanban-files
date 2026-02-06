using System.Collections.ObjectModel;
using KanbanFiles.Services;

namespace KanbanFiles.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        private readonly BoardConfigService _boardConfigService;
        private readonly FileSystemService _fileSystemService;
        private Models.Board? _board;

        public MainViewModel()
        {
            Title = "Home";
            _boardConfigService = new BoardConfigService();
            _fileSystemService = new FileSystemService();
            Columns = new ObservableCollection<ColumnViewModel>();
        }

        public ObservableCollection<ColumnViewModel> Columns { get; }

        [ObservableProperty]
        private string _boardName = "Kanban Board";

        [ObservableProperty]
        private bool _isLoaded = false;

        public event EventHandler? OpenFolderRequested;
        public event EventHandler<string>? AddColumnRequested;

        [RelayCommand]
        private void OpenFolder()
        {
            OpenFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        public async Task LoadBoardAsync(string folderPath)
        {
            _board = await _boardConfigService.LoadOrInitializeAsync(folderPath);
            var columns = await _fileSystemService.EnumerateColumnsAsync(_board);
            
            Columns.Clear();
            foreach (var column in columns)
            {
                var columnViewModel = new ColumnViewModel(column, _fileSystemService, _boardConfigService, _board);
                columnViewModel.DeleteRequested += OnColumnDeleteRequested;
                Columns.Add(columnViewModel);
            }
            
            BoardName = _board.Name;
            IsLoaded = true;
        }

        [RelayCommand]
        private void AddColumn()
        {
            AddColumnRequested?.Invoke(this, string.Empty);
        }

        public async Task CreateColumnAsync(string columnName)
        {
            if (_board == null) return;

            var sanitizedName = SanitizeFolderName(columnName);
            await _fileSystemService.CreateColumnFolderAsync(_board.RootPath, sanitizedName);

            var newColumnConfig = new Models.ColumnConfig
            {
                FolderName = sanitizedName,
                DisplayName = columnName,
                SortOrder = _board.Columns.Count,
                ItemOrder = new List<string>()
            };

            _board.Columns.Add(newColumnConfig);
            await _boardConfigService.SaveAsync(_board);

            var column = new Models.Column
            {
                Name = columnName,
                FolderPath = Path.Combine(_board.RootPath, sanitizedName),
                SortOrder = newColumnConfig.SortOrder
            };

            var columnViewModel = new ColumnViewModel(column, _fileSystemService, _boardConfigService, _board);
            columnViewModel.DeleteRequested += OnColumnDeleteRequested;
            Columns.Add(columnViewModel);
        }

        private void OnColumnDeleteRequested(object? sender, string folderPath)
        {
            var columnViewModel = Columns.FirstOrDefault(c => c.FolderPath == folderPath);
            if (columnViewModel != null)
            {
                Columns.Remove(columnViewModel);
            }
        }

        public async Task ReorderColumnAsync(ColumnViewModel sourceColumn, int targetIndex)
        {
            if (_board == null) return;

            // Get current index
            var currentIndex = Columns.IndexOf(sourceColumn);
            if (currentIndex == -1) return;

            // Adjust target index if moving down
            if (targetIndex > currentIndex)
            {
                targetIndex--;
            }

            // Don't move if dropping at same position
            if (currentIndex == targetIndex)
                return;

            // Move in the observable collection
            Columns.Move(currentIndex, targetIndex);

            // Update SortOrder for all columns
            for (int i = 0; i < Columns.Count; i++)
            {
                var columnViewModel = Columns[i];
                var folderName = Path.GetFileName(columnViewModel.FolderPath);
                var columnConfig = _board.Columns.FirstOrDefault(c => c.FolderName == folderName);
                if (columnConfig != null)
                {
                    columnConfig.SortOrder = i;
                }
            }

            // Save to config
            await _boardConfigService.SaveAsync(_board);
        }

        private string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
            return string.IsNullOrWhiteSpace(sanitized) ? "New Column" : sanitized;
        }
    }
}
