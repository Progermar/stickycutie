using System.Windows.Documents;
using System.Windows.Markup;

namespace StickyCutie.Wpf;

static class NoteDocumentBuilder
{
    public static string FromPlainText(string? text)
    {
        var paragraph = new Paragraph();
        if (!string.IsNullOrWhiteSpace(text))
        {
            paragraph.Inlines.Add(new Run(text));
        }
        else
        {
            paragraph.Inlines.Add(new Run());
        }

        var doc = new FlowDocument(paragraph);
        return XamlWriter.Save(doc);
    }
}
