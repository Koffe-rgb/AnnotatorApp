using System.Windows.Documents;

namespace ComponentLibrary.Tools
{
    /// <summary>
    /// Класс от разработчика WPF Праджакта Джоши (Prajakta Joshi)
    /// для обнаружения промежутков между словами в потоковых документах
    /// </summary>
    public static class WordBreaker
    {
        public static TextRange GetWordRange(TextPointer position)
        {
            TextRange wordRange = null;
            TextPointer wordStartPosition = null;
            // Go forward first, to find word end position.
            var wordEndPosition = GetPositionAtWordBoundary(position, LogicalDirection.Forward);
            if (wordEndPosition != null)
            {
                // Then travel backwards, to find word start position.
                wordStartPosition = GetPositionAtWordBoundary(wordEndPosition, LogicalDirection.Backward);
            }
            if (wordStartPosition != null && wordEndPosition != null)
            {
                wordRange = new TextRange(wordStartPosition, wordEndPosition);
            }

            return wordRange;
        }

        private static TextPointer GetPositionAtWordBoundary(TextPointer position, LogicalDirection wordBreakDirection)
        {
            if (!position.IsAtInsertionPosition)
            {
                position = position.GetInsertionPosition(wordBreakDirection);
            }
            var navigator = position;
            while (navigator != null && !IsPositionNextToWordBreak(navigator, wordBreakDirection))
            {
                navigator = navigator.GetNextInsertionPosition(wordBreakDirection);
            }
            return navigator;
        }

        private static bool IsPositionNextToWordBreak(TextPointer position, LogicalDirection wordBreakDirection)
        {
            var isAtWordBoundary = false;
            // Skip over any formatting.
            if (position.GetPointerContext(wordBreakDirection) != TextPointerContext.Text)
            {
                position = position.GetInsertionPosition(wordBreakDirection);
            }
            if (position.GetPointerContext(wordBreakDirection) == TextPointerContext.Text)
            {
                var oppositeDirection = wordBreakDirection == LogicalDirection.Forward ?
                    LogicalDirection.Backward : 
                    LogicalDirection.Forward;
                var runBuffer = new char[1];
                var oppositeRunBuffer = new char[1];
                position.GetTextInRun(wordBreakDirection, runBuffer,0, 1);
                position.GetTextInRun(oppositeDirection, oppositeRunBuffer, 0, 1);
                if (runBuffer[0] == ' ' && oppositeRunBuffer[0] != ' ')
                {
                    isAtWordBoundary = true;
                }
            }
            else
            {
                // If we're not adjacent to text then we always want to consider this position a "word break".
                // In practice, we're most likely next to an embedded object or a block boundary.
                isAtWordBoundary = true;
            }
            return isAtWordBoundary;
        }
    }
}