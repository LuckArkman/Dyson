namespace WinApp.Resources.Models;

public partial class SidebarMenuItem : ContentView
{
    // Esta propriedade permite que o XAML resolva o símbolo 'Title'
    public static readonly BindableProperty TitleProperty = 
        BindableProperty.Create(nameof(Title), typeof(string), typeof(SidebarMenuItem), string.Empty);

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public SidebarMenuItem()
    {
        InitializeComponent();
    }

    private void OnToggleClicked(object sender, EventArgs e)
    {
        // Alterna a visibilidade para o efeito cascata
        ChildContainer.IsVisible = !ChildContainer.IsVisible;
    }
}