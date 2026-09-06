export interface PayloadAnalysis {
  rawByteSize: number;
  formattedByteSize: number;
  minifiedByteSize: number;
  estimatedGzipByteSize: number;
  compressionRatioPercent: number;
  totalKeys: number;
  maxDepth: number;
  arrayCount: number;
  objectCount: number;
  stringCount: number;
  numberCount: number;
  booleanCount: number;
  nullCount: number;
  nonAsciiCharacterCount: number;
  isMultibyte: boolean;
  characterEncoding: string;
  inferredSchemaJson: string;
}

export interface JsonPathMatchItem {
  path: string;
  valueJson: string;
  valueType: string;
}

export interface JsonPathEvaluation {
  isValid: boolean;
  errorMessage?: string | null;
  matchCount: number;
  matches: JsonPathMatchItem[];
}

export type DiffType = 'Added' | 'Removed' | 'Modified' | 'Unchanged';

export interface PayloadDiffEntry {
  path: string;
  diffType: DiffType;
  leftValue?: string | null;
  rightValue?: string | null;
}

export interface PayloadDiff {
  hasDifferences: boolean;
  addedCount: number;
  removedCount: number;
  modifiedCount: number;
  unchangedCount: number;
  entries: PayloadDiffEntry[];
  leftByteSize: number;
  rightByteSize: number;
  byteSizeDelta: number;
}

export interface PayloadSchemaValidation {
  isValid: boolean;
  validationErrors: string[];
  structuralWarnings: string[];
}
