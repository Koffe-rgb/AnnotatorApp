using System.Collections.ObjectModel;

namespace ComponentLibrary.Model
{
    public class Node
    {
        public string StyleName { get; set; }
        public ObservableCollection<Bookmark> StyledBookmarks { get; set; }
        public Node(string styleName)
        {
            StyleName = styleName;
            StyledBookmarks = new ObservableCollection<Bookmark>();
        }
    }
}