﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace EditorConfig
{
    public class FilteredCompletionSet : CompletionSet2
    {
        public FilteredObservableCollection<Completion> currentCompletions;
        private BulkObservableCollection<Completion> _completions = new BulkObservableCollection<Completion>();
        public List<string> _activeFilters = new List<string>();
        private string _typed;
        private static List<Span> _defaultEmptyList = new List<Span>();

        public FilteredCompletionSet(ITrackingSpan applicableTo, IEnumerable<Completion> completions, IEnumerable<Completion> completionBuilders, IReadOnlyList<IIntellisenseFilter> filters)
            : base("All", "All", applicableTo, completions, completionBuilders, filters)
        {
            _completions.AddRange(completions);
            currentCompletions = new FilteredObservableCollection<Completion>(_completions);
        }

        public override IList<Completion> Completions
        {
            get { return currentCompletions; }
        }

        public override void Filter()
        {
            // This is handled in SelectBestMatch
        }

        public override void SelectBestMatch()
        {
            _typed = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);
            var currentActiveFilters = Filters;

            if (currentActiveFilters != null && currentActiveFilters.Count > 0)
            {
                var activeFilters = currentActiveFilters.Where(f => f.IsChecked).Select(f => f.AutomationText);

                if (!activeFilters.Any())
                    activeFilters = currentActiveFilters.Select(f => f.AutomationText);

                _activeFilters.Clear();
                _activeFilters.AddRange(activeFilters);

                currentCompletions.Filter(new Predicate<Completion>(DoesCompletionMatchAutomationText));
            }

            var ordered = currentCompletions.OrderByDescending(c => GetHighlightedSpansInDisplayText(c.DisplayText).Sum(s => s.Length));

            if (ordered.Any())
            {
                var count = ordered.Count();
                SelectionStatus = new CompletionSelectionStatus(ordered.First(), count == 1, count == 1);
            }
            else
            {
                SelectBestMatch(CompletionMatchType.MatchDisplayText, false);
            }
        }

        private bool DoesCompletionMatchAutomationText(Completion completion)
        {
            return _activeFilters.Exists(x =>
                x.Equals(completion.IconAutomationText, StringComparison.OrdinalIgnoreCase)) &&
                GetHighlightedSpansInDisplayText(completion.DisplayText).Count > 0;
        }

        public override IReadOnlyList<Span> GetHighlightedSpansInDisplayText(string displayText)
        {
            return GetHighlightedSpans(displayText, _typed);
        }

        public static List<Span> GetHighlightedSpans(string displayText, string typed)
        {
            var matches = new SortedList<int, Span>();
            string match = string.Empty;
            int startIndex = 0;

            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];

                if (!displayText.Contains(match + c))
                {
                    if (!matches.Any())
                        return _defaultEmptyList;

                    match = string.Empty;
                    startIndex = matches.Last().Value.End;
                }

                var current = match + c;
                var index = displayText.IndexOf(current, startIndex);
                var offset = 0;

                if (index == -1)
                    return _defaultEmptyList;

                if (index > 0)
                {
                    index = displayText.IndexOf("_" + current, startIndex);
                    offset = 1;
                }

                if (index > -1)
                {
                    matches[index] = new Span(index + offset, current.Length);
                    match += c;
                }
                else
                {
                    return _defaultEmptyList;
                }
            }

            return matches.Values.ToList();
        }
    }
}
