using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;

namespace AgroAdd.Extensions
{
    public static class HtmlDocumentExtensions
    {

        public static IEnumerable<HtmlElement> ElementsByClass(this HtmlDocument doc, string type, string className)
        {
            foreach (HtmlElement e in doc.GetElementsByTagName(type) )
                if (e.GetAttribute("className")?.Split(' ')?.Any(x => x == className) == true)
                    yield return e;
        }
    }
}
