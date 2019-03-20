using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;

namespace AgroAdd.Extensions
{
    public static class HtmlElementExtensions
    {

        public static IEnumerable<HtmlElement> ElementsByClass(this HtmlElement element, string type, string className)
        {
            foreach (HtmlElement e in element.GetElementsByTagName(type) )
                if (e.GetAttribute("className")?.Split(' ')?.Any(x => x == className) == true)
                    yield return e;
        }


    }
}
