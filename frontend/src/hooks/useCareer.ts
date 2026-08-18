import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { careerService } from '@/services/careerService';
import type { AiResult, CareerProfile, ResumeAnalysis, UpdateCareerProfilePayload } from '@/types/api';

import { queryKeys } from './queryKeys';

export function useCareerProfile() {
  return useQuery({
    queryKey: queryKeys.career.profile,
    queryFn: () => careerService.getProfile(),
  });
}

export function useUpdateCareerProfile() {
  const client = useQueryClient();
  return useMutation<CareerProfile, Error, UpdateCareerProfilePayload>({
    mutationFn: (payload) => careerService.updateProfile(payload),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.career.all }),
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}

/**
 * AI-разбор резюме.
 *
 * Результат сохраняется в поле `aiReview` профиля, поэтому кэш профиля после
 * успеха обязательно сбрасывается — иначе разбор виден на экране, но
 * исчезает при следующем открытии страницы.
 */
export function useAnalyzeResume() {
  const client = useQueryClient();
  return useMutation<AiResult<ResumeAnalysis>, Error, void>({
    mutationFn: () => careerService.analyzeResume(),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.career.all }),
        client.invalidateQueries({ queryKey: queryKeys.ai.all }),
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}
