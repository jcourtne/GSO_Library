export const SCORE_PART_TYPES = {
  CONDUCTOR_SCORE: 'conductor_score',
  INSTRUMENT_PART: 'instrument_part',
  PERCUSSION_PART: 'percussion_part',
  VOICE_PART:      'voice_part',
  UNLISTED_PART:   'unlisted_part',
} as const;

export type ScorePartType = typeof SCORE_PART_TYPES[keyof typeof SCORE_PART_TYPES];
