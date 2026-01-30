using System.Collections.Generic;

namespace SAS.Utilities.DeveloperConsole
{
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
                if (!node.Children.TryGetValue(c, out var next))
                {
                    next = new TrieNode();
                    node.Children[c] = next;
                }

                node = next;
            }

            node.IsEndOfWord = true;
            node.Command = command;
        }

        public void Remove(string command)
        {
            RemoveInternal(_root, command.ToLowerInvariant(), 0);
        }

        private bool RemoveInternal(TrieNode node, string word, int index)
        {
            if (index == word.Length)
            {
                if (!node.IsEndOfWord)
                    return false;

                node.IsEndOfWord = false;
                node.Command = null;

                // tell parent whether this node can be deleted
                return node.Children.Count == 0;
            }

            char c = word[index];

            if (!node.Children.TryGetValue(c, out var child))
                return false;

            bool shouldDeleteChild = RemoveInternal(child, word, index + 1);

            if (shouldDeleteChild)
                node.Children.Remove(c);

            return node.Children.Count == 0 && !node.IsEndOfWord;
        }

        public List<string> GetAllWithPrefix(string prefix)
        {
            TrieNode node = _root;

            foreach (char c in prefix.ToLowerInvariant())
            {
                if (!node.Children.TryGetValue(c, out node))
                    return new List<string>();
            }

            var results = new List<string>();
            CollectAllCommands(node, results);
            return results;
        }

        private void CollectAllCommands(TrieNode node, List<string> results)
        {
            if (node.IsEndOfWord && node.Command != null)
                results.Add(node.Command);

            foreach (var kvp in node.Children)
                CollectAllCommands(kvp.Value, results);
        }
    }
}
