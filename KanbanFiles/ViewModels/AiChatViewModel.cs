using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.Foundation;

namespace KanbanFiles.ViewModels;

public partial class AiChatViewModel : ObservableObject
{
    private LanguageModel? _languageModel;
    private LanguageModelContext? _context;
    private DateTime _lastUiUpdate = DateTime.MinValue;
    private const int UI_UPDATE_THROTTLE_MS = 50;
    private ChatMessage? _lastAssistantMessage;

    [ObservableProperty]
    private bool _isModelAvailable;

    [ObservableProperty]
    private bool _isModelLoading;

    [ObservableProperty]
    private string _statusMessage = "Checking AI availability...";

    [ObservableProperty]
    private string _userInput = string.Empty;

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string? _lastAssistantResponse;

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public Func<string>? GetEditorContent { get; set; }
    public Action<string>? ApplyToEditor { get; set; }

    public async Task InitializeAsync()
    {
        IsModelLoading = true;
        StatusMessage = "Checking AI availability...";
        Debug.WriteLine("[AiChatViewModel] Starting AI model initialization...");

        try
        {
            Debug.WriteLine("[AiChatViewModel] Calling LanguageModel.GetReadyState()...");
            AIFeatureReadyState readyState = LanguageModel.GetReadyState();
            Debug.WriteLine($"[AiChatViewModel] ReadyState: {readyState}");

            if (readyState == AIFeatureReadyState.NotReady)
            {
                Debug.WriteLine("[AiChatViewModel] Model not ready, calling EnsureReadyAsync()...");
                StatusMessage = "Preparing AI model...";

                AIFeatureReadyResult result = await LanguageModel.EnsureReadyAsync();
                Debug.WriteLine($"[AiChatViewModel] EnsureReadyAsync result: Status={result.Status}");

                if (result.Status != AIFeatureReadyResultState.Success)
                {
                    string errorMsg = $"AI model preparation failed. Status: {result.Status}";
                    Debug.WriteLine($"[AiChatViewModel] ERROR: {errorMsg}");
                    StatusMessage = errorMsg;
                    IsModelAvailable = false;
                    IsModelLoading = false;
                    return;
                }

                Debug.WriteLine("[AiChatViewModel] Model prepared successfully.");
            }
            else if (readyState != AIFeatureReadyState.Ready)
            {
                string errorMsg = $"AI model not available. ReadyState: {readyState}";
                Debug.WriteLine($"[AiChatViewModel] ERROR: {errorMsg}");
                StatusMessage = errorMsg;
                IsModelAvailable = false;
                IsModelLoading = false;
                return;
            }

            Debug.WriteLine("[AiChatViewModel] Creating LanguageModel instance...");
            _languageModel = await LanguageModel.CreateAsync();
            Debug.WriteLine($"[AiChatViewModel] LanguageModel created: {_languageModel != null}");

            Debug.WriteLine("[AiChatViewModel] Creating context...");
            _context = _languageModel.CreateContext();
            Debug.WriteLine($"[AiChatViewModel] Context created: {_context != null}");

            IsModelAvailable = true;
            StatusMessage = string.Empty;
            Debug.WriteLine("[AiChatViewModel] AI model initialization completed successfully!");
        }
        catch (Exception ex)
        {
            string errorMsg = $"Failed to initialize AI: {ex.GetType().Name}: {ex.Message}";
            Debug.WriteLine($"[AiChatViewModel] EXCEPTION: {errorMsg}");
            Debug.WriteLine($"[AiChatViewModel] Stack trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Debug.WriteLine($"[AiChatViewModel] Inner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }

            StatusMessage = errorMsg;
            IsModelAvailable = false;
        }
        finally
        {
            IsModelLoading = false;
            Debug.WriteLine($"[AiChatViewModel] Initialization complete. IsModelAvailable={IsModelAvailable}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_languageModel == null || _context == null || string.IsNullOrWhiteSpace(UserInput))
            return;

        string userText = UserInput.Trim();
        UserInput = string.Empty;

        await SendPromptAsync(userText);
    }

    private bool CanSend() => IsModelAvailable && !IsGenerating && !string.IsNullOrWhiteSpace(UserInput);

    partial void OnUserInputChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsModelAvailableChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnLastAssistantResponseChanged(string? value) => ApplyResponseCommand.NotifyCanExecuteChanged();

    public async Task SendQuickActionAsync(string prompt)
    {
        if (_languageModel == null || _context == null || IsGenerating)
            return;

        await SendPromptAsync(prompt);
    }

    private async Task SendPromptAsync(string userText)
    {
        Debug.WriteLine($"[AiChatViewModel] SendPromptAsync called with: {userText}");

        string fileContent = GetEditorContent?.Invoke() ?? string.Empty;

        string fullPrompt = string.IsNullOrEmpty(fileContent)
            ? userText
            : $"Here is the current file content:\n```\n{fileContent}\n```\n\nUser request: {userText}";

        Debug.WriteLine($"[AiChatViewModel] Full prompt length: {fullPrompt.Length} characters");

        ChatMessage userMessage = new() { Role = "user", Text = userText };
        Messages.Add(userMessage);

        ChatMessage assistantMessage = new() { Role = "assistant", Text = string.Empty };
        Messages.Add(assistantMessage);
        _lastAssistantMessage = assistantMessage;

        IsGenerating = true;
        LastAssistantResponse = null;

        try
        {
            Debug.WriteLine("[AiChatViewModel] Starting GenerateResponseAsync...");
            IAsyncOperationWithProgress<LanguageModelResponseResult, string> operation = _languageModel!.GenerateResponseAsync(_context!, fullPrompt, new LanguageModelOptions());

            operation.Progress = (_, progressText) =>
            {
                DateTime now = DateTime.UtcNow;
                if ((now - _lastUiUpdate).TotalMilliseconds < UI_UPDATE_THROTTLE_MS)
                {
                    return;
                }
                _lastUiUpdate = now;

                App.MainDispatcher?.TryEnqueue(() =>
                {
                    assistantMessage.Text += progressText;
                });
            };

            LanguageModelResponseResult result = await operation;
            Debug.WriteLine($"[AiChatViewModel] Response received, length: {result.Text?.Length ?? 0}");

            // Ensure final text is set
            assistantMessage.Text = result.Text;
            LastAssistantResponse = result.Text;
        }
        catch (Exception ex)
        {
            string errorMsg = $"Error: {ex.GetType().Name}: {ex.Message}";
            Debug.WriteLine($"[AiChatViewModel] Generation error: {errorMsg}");
            Debug.WriteLine($"[AiChatViewModel] Stack trace: {ex.StackTrace}");
            assistantMessage.Text = errorMsg;
        }
        finally
        {
            IsGenerating = false;
            Debug.WriteLine("[AiChatViewModel] SendPromptAsync completed.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyResponse))]
    private void ApplyResponse()
    {
        if (LastAssistantResponse != null && _lastAssistantMessage != null && !_lastAssistantMessage.HasBeenApplied)
        {
            ApplyToEditor?.Invoke(LastAssistantResponse);
            _lastAssistantMessage.HasBeenApplied = true;
            ApplyResponseCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanApplyResponse() => 
        LastAssistantResponse != null && 
        _lastAssistantMessage != null && 
        !_lastAssistantMessage.HasBeenApplied;

    [RelayCommand]
    private void ClearChat()
    {
        Messages.Clear();
        LastAssistantResponse = null;
        _lastAssistantMessage = null;

        // Reset context for a fresh conversation
        if (_languageModel != null)
        {
            _context?.Dispose();
            _context = _languageModel.CreateContext();
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
        _languageModel?.Dispose();
        _context = null;
        _languageModel = null;
    }
}
