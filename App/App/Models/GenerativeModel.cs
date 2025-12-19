using Interfaces;

namespace Models;

public record GenerativeModel(
    int inputSize,
    int hiddenSize,
    int outputSize,
    string datasetPath,
    string modelPath,
    ISearchService searchService,
    int contextWindowSize = 5);