using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Idefix.App.Controls;
using Idefix.App.Services;
using Idefix.App.ViewModels;
using Windows.Media.SpeechRecognition;
using Windows.System;

namespace Idefix.App.Views;

public sealed partial class ChatView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ChatViewModel), typeof(ChatView), new PropertyMetadata(null, OnViewModelChanged));

    public ChatView()
    {
        this.InitializeComponent();
    }

    public event EventHandler<CitationEventArgs>? CitationRequested;

    public event EventHandler? ProfileRequested;

    public ChatViewModel ViewModel
    {
        get => (ChatViewModel)this.GetValue(ViewModelProperty);
        set => this.SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (ChatView)d;
        if (e.OldValue is ChatViewModel old)
        {
            old.PropertyChanged -= view.OnViewModelPropertyChanged;
            old.Messages.CollectionChanged -= view.OnMessagesChanged;
        }

        if (e.NewValue is ChatViewModel current)
        {
            current.PropertyChanged += view.OnViewModelPropertyChanged;
            current.Messages.CollectionChanged += view.OnMessagesChanged;
        }

        view.PlaceInput();
        view.PromptBox.Focus(FocusState.Programmatic);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatViewModel.IsEmpty))
        {
            this.PlaceInput();
        }
    }

    private void OnMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        this.DispatcherQueue.TryEnqueue(() => this.Scroller.ChangeView(null, this.Scroller.ScrollableHeight, null, disableAnimation: false));

    private void PlaceInput() =>
        this.BottomSpacer.Height = this.ViewModel?.IsEmpty != false ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    private void OnPromptKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && this.ViewModel.SendCommand.CanExecute(null))
        {
            e.Handled = true;
            this.ViewModel.SendCommand.Execute(null);
        }
    }

    private void OnCitationClicked(object? sender, CitationEventArgs e) => this.CitationRequested?.Invoke(this, e);

    private void OnOpenProfileClicked(object sender, RoutedEventArgs e) => this.ProfileRequested?.Invoke(this, EventArgs.Empty);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Speech availability depends on OS privacy settings; failures only show a hint.")]
    private async void OnDictateClicked(object sender, RoutedEventArgs e)
    {
        this.DictationError.IsOpen = false;
        try
        {
            using var recognizer = Strings.LanguageTag is { } tag
                ? new SpeechRecognizer(new Windows.Globalization.Language(tag))
                : new SpeechRecognizer();
            recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "dictation"));
            await recognizer.CompileConstraintsAsync();
            var result = await recognizer.RecognizeAsync();
            if (result.Status == SpeechRecognitionResultStatus.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                this.ViewModel.Prompt = string.IsNullOrWhiteSpace(this.ViewModel.Prompt) ? result.Text : $"{this.ViewModel.Prompt} {result.Text}";
            }
        }
        catch (Exception)
        {
            this.DictationError.IsOpen = true;
        }
    }
}
