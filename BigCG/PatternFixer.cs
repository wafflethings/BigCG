using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BigCG;

public static class PatternFixer
{
    public static string[] FixPatterns(string[] pattern)
    {
        List<string> result = new();
        List<string> environment = new();
        List<string> enemies = new();
        List<string> targetList = environment;

        foreach (string line in pattern)
        {
            if (line == string.Empty)
            {
                targetList = enemies;
                continue;
            }

            targetList.Add(line);
        }

        result.AddRange(FixPatternHeights(environment));
        result.Add(string.Empty);
        result.AddRange(FixPatternEnemies(enemies));

        Debug.Log($"Pattern ({result.Count} lines)");
        foreach (string line in result)
        {
            Debug.Log($"    {line} ({line.Length})");
        }

        return result.ToArray();
    }

    private static List<string> FixPatternHeights(List<string> pattern)
    {
        List<List<int>> parsedPattern = ParseStringArray(pattern);

        foreach (List<int> row in parsedPattern)
        {
            if (row.Count < Plugin.GridSize)
            {
                int indexColumn = 0;
                while (row.Count != Plugin.GridSize)
                {
                    row.Add(row[indexColumn]);
                    indexColumn++;
                }
            }
            else if (row.Count > Plugin.GridSize)
            {
                int difference = row.Count - Plugin.GridSize;
                row.RemoveRange(row.Count - difference - 1, difference);
            }
        }

        if (parsedPattern.Count < Plugin.GridSize)
        {
            int indexRow = 0;
            while (parsedPattern.Count != Plugin.GridSize)
            {
                parsedPattern.Add(parsedPattern[indexRow]);
                indexRow++;
            }
        }
        else if (parsedPattern.Count > Plugin.GridSize)
        {
            int difference = parsedPattern.Count - Plugin.GridSize;
            parsedPattern.RemoveRange(parsedPattern.Count - difference , difference);
        }

        return ListIntPatternToStringListPattern(parsedPattern);
    }

    private static List<string> FixPatternEnemies(List<string> pattern)
    {
        List<string> result = new();

        foreach (string row in pattern)
        {
            string newRow = row;

            if (newRow.Length < Plugin.GridSize)
            {
                int indexColumn = 0;
                while (newRow.Length != Plugin.GridSize)
                {
                    newRow += newRow[indexColumn];
                    indexColumn++;
                }
            }
            else if (row.Length > Plugin.GridSize)
            {
                newRow = newRow.Substring(0, Plugin.GridSize);
            }

            result.Add(newRow);
        }

        if (result.Count < Plugin.GridSize)
        {
            int indexRow = 0;
            while (result.Count != Plugin.GridSize)
            {
                result.Add(result[indexRow]);
                indexRow++;
            }
        }
        else if (result.Count > Plugin.GridSize)
        {
            int difference = result.Count - Plugin.GridSize;
            result.RemoveRange(result.Count - difference - 1, difference);
        }

        return result;
    }

    private static List<List<int>> ParseStringArray(List<string> pattern)
    {
        List<List<int>> result = new();

        foreach (string row in pattern)
        {
            if (!CorrectlyClosedBrackets(row))
            {
                Debug.LogError("Brackets don't close in pattern.");
                return null;
            }

            if (!MinusesInValidPositions(row))
            {
                Debug.LogError("Minuses are invalid in pattern.");
                return null;
            }

            List<int> rowList = new();
            result.Add(rowList);

            bool insideBracket = false;
            bool hasAdded = false;
            bool isNegative = false;
            foreach (char character in row)
            {
                //dont need to check if its opening or closing since CorrectlyClosedBrackets does that
                if (character is '(' or ')')
                {
                    hasAdded = false;
                    insideBracket = !insideBracket;
                    continue;
                }

                if (!insideBracket)
                {
                    rowList.Add(int.Parse(character.ToString()));
                }
                else
                {
                    if (!hasAdded)
                    {
                        if (character == '-')
                        {
                            isNegative = true;
                            continue;
                        }
                        rowList.Add(int.Parse(character.ToString()));
                        hasAdded = true;
                    }
                    else
                    {
                        rowList[rowList.Count - 1] = int.Parse(string.Concat(rowList[rowList.Count - 1], character));

                        if (isNegative)
                        {
                            rowList[rowList.Count - 1] *= -1;
                            isNegative = false;
                        }
                    }
                }
            }
        }

        return result;
    }

    private static List<string> ListIntPatternToStringListPattern(List<List<int>> pattern)
    {
        List<string> result = new();

        foreach (List<int> row in pattern)
        {
            string rowString = string.Empty;

            foreach (int number in row)
            {
                if (number.ToString().Length == 1)
                {
                    rowString += number;
                }
                else
                {
                    rowString += $"({number})";
                }
            }

            result.Add(rowString);
        }

        return result;
    }

    private static bool CorrectlyClosedBrackets(string line) => Regex.Matches(line, @"\(").Count == Regex.Matches(line, @"\)").Count;

    private static bool MinusesInValidPositions(string line)
    {
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] != '-')
            {
                continue;
            }

            //character after minus is a digit
            if (char.IsDigit(line[i + 1]))
            {
                continue;
            }

            //character before minus is a (
            if (line[i - 1] != '(')
            {
                return false;
            }
        }

        return true;
    }
}
