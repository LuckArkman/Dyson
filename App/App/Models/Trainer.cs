namespace Models;

public record Trainer(string datasetPath,int epochs, float learningRate, float validationSplit, int batchSize);