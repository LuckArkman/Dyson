using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinApp.Resources.UIs.MarketPlace;

public partial class Navegar : ContentPage
{
    public ObservableCollection<Produto> Items { get; } = new();
    public Navegar()
    {
        InitializeComponent();
        BindingContext = this;
        ItemsSource();
    }

    private void OnToggleClicked(object? sender, TappedEventArgs e)
    {
    }

    public IEnumerable<Produto> ItemsSource()
    {
        Items.Clear();

        Items.Add(new Produto{ Nome = "Espada Longa", Preco = 150 });
        Items.Add(new Produto{ Nome = "Escudo de Ferro", Preco = 90 });
        Items.Add(new Produto{ Nome = "Poção de Vida", Preco = 25 });
        Items.Add(new Produto{ Nome = "Espada Longa", Preco = 150 });
        Items.Add(new Produto{ Nome = "Escudo de Ferro", Preco = 90 });
        Items.Add(new Produto{ Nome = "Poção de Vida", Preco = 25 });
        Items.Add(new Produto{ Nome = "Espada Longa", Preco = 150 });
        Items.Add(new Produto{ Nome = "Escudo de Ferro", Preco = 90 });
        Items.Add(new Produto{ Nome = "Poção de Vida", Preco = 25 });
        return Items;
    }
}

public class Produto 
{
    public string Nome { get; set; }
    public int Preco { get; set; }
}