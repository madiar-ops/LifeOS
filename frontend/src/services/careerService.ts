import { api } from '@/lib/httpClient';
import type {
  AiResult,
  CareerProfile,
  ResumeAnalysis,
  UpdateCareerProfilePayload,
} from '@/types/api';

/** Карьера: `CareerController`. */
export const careerService = {
  /**
   * Профиль карьеры.
   *
   * Создаётся лениво, при первом обращении (ADR 77) — поэтому GET никогда не
   * отдаёт 404, и интерфейсу не нужна отдельная ветка «профиля ещё нет».
   */
  getProfile(): Promise<CareerProfile> {
    return api.get<CareerProfile>('/career/profile');
  },

  updateProfile(payload: UpdateCareerProfilePayload): Promise<CareerProfile> {
    return api.put<CareerProfile>('/career/profile', payload);
  },

  /**
   * AI-разбор резюме. Текст извлекается из привязанного PDF на бэкенде.
   *
   * Результат сохраняется в поле `aiReview` профиля, поэтому после успеха
   * кэш профиля нужно инвалидировать.
   */
  analyzeResume(): Promise<AiResult<ResumeAnalysis>> {
    return api.post<AiResult<ResumeAnalysis>>('/career/resume-analysis');
  },
};
