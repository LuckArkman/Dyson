using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinApp.Resources.UIs;

public partial class DashBoard : ContentPage
{
    public DashBoard()
    {
        InitializeComponent();
    }

    public void OnToggleClicked(object? sender, TappedEventArgs e)
    {
        if (sender is Label label) Debug.WriteLine($"[MENU CLICK] {label.Text}");
        else
            Debug.WriteLine("[MENU CLICK] Item desconhecido");
    }

    private void OnButtonClicked(object sender, EventArgs e)
    => Debug.WriteLine("BOTÃO FUNCIONANDO");
}