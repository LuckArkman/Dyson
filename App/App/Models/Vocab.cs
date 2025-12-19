namespace Models;

public class Vocab
{
    public int id { get; set; }
    public string text { get; set; }

    public Vocab(int id, string text)
    {
        this.id = id;
        this.text = text;
    }
}