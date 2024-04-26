// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

// Extends System.Linq namespace so using the same here.
namespace System.Linq;

/// <summary>
/// Contains extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class EnumerableExtensions
{
    /// <summary>
    /// Split the given <see cref="IEnumerable{T}"/> into batches of the given size.
    /// </summary>
    /// <remarks>
    /// The last batch may not be the same size as the others since it will contain the remaining elements.
    /// </remarks>
    /// <typeparam name="T">The type of the contents of the <see cref="IEnumerable{T}"/>.</typeparam>
    /// <param name="listToSplit">The list of items to split into separate batches.</param>
    /// <param name="batchSize">The number of items per batch.</param>
    /// <returns>The given <see cref="IEnumerable{T}"/> split into batches of <paramref name="batchSize"/>.</returns>
    public static IEnumerable<IEnumerable<T>> SplitIntoBatches<T>(this IEnumerable<T> listToSplit, int batchSize)
    {
        if (listToSplit == null)
        {
            throw new ArgumentNullException(nameof(listToSplit));
        }

        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
        }

        var enumerator = listToSplit.GetEnumerator();
        while (enumerator.MoveNext())
        {
            yield return GetBatch(enumerator, batchSize);
        }
    }

    /// <summary>
    /// Produce an <see cref="IEnumerable{T}"/> by taking the next <paramref name="batchSize"/> elements from the
    /// given enumerator, or less if the <see cref="IEnumerator{T}"/> has fewer items than requested.
    /// </summary>
    /// <typeparam name="T">The type of the contents of the <see cref="IEnumerator{T}"/>.</typeparam>
    /// <param name="enumerator">The enumerator to take the next <paramref name="batchSize"/> elements from.</param>
    /// <param name="batchSize">The number of items to take from the <paramref name="enumerator"/>.</param>
    /// <returns>The requested number of items from the given <see cref="IEnumerator{T}"/>, or less
    /// if the <see cref="IEnumerator{T}"/> has fewer items than requested.</returns>
    private static IEnumerable<T> GetBatch<T>(IEnumerator<T> enumerator, int batchSize)
    {
        // Return the first item from the enumerator, since we already moved next in the SplitIntoBatches method.
        var batchCounter = 1;
        yield return enumerator.Current;

        while (batchCounter < batchSize && enumerator.MoveNext())
        {
            yield return enumerator.Current;
            batchCounter++;
        }
    }
}
