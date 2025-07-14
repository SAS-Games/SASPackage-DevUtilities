using System.Collections.Generic;


public class CommandSuggester
{
    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children = new();
        public bool IsEndOfWord;
        public string Command;
    }
    
    private readonly TrieNode _root = new();

    public void Insert(string command)
    {
        TrieNode node = _root;
        foreach (char c in command.ToLowerInvariant())
        {
            if (!node.Children.ContainsKey(c))
                node.Children[c] = new TrieNode();

            node = node.Children[c];
        }
        node.IsEndOfWord = true;
        node.Command = command;
    }

    public List<string> GetAllWithPrefix(string prefix)
    {
        TrieNode node = _root;

        foreach (char c in prefix.ToLowerInvariant())
        {
            if (!node.Children.TryGetValue(c, out node))
                return new List<string>(); // No suggestions
        }

        var results = new List<string>();
        CollectAllCommands(node, results);
        return results;
    }

    private void CollectAllCommands(TrieNode node, List<string> results)
    {
        if (node.IsEndOfWord)
            results.Add(node.Command);

        foreach (var kvp in node.Children)
        {
            CollectAllCommands(kvp.Value, results);
        }
    }
}