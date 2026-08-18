import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { studyService } from '@/services/studyService';
import type {
  AiResult,
  CreateStudyMaterialPayload,
  GenerateQuizPayload,
  PaginationParams,
  Quiz,
  QuizGrade,
  StudyMaterial,
  StudyNote,
  StudySummary,
  Uuid,
} from '@/types/api';

import { queryKeys } from './queryKeys';

// ---- Материалы -----------------------------------------------------------

export function useStudyMaterials(query: PaginationParams) {
  return useQuery({
    queryKey: queryKeys.study.materials(query),
    queryFn: () => studyService.listMaterials(query),
    placeholderData: keepPreviousData,
  });
}

export function useStudyMaterial(id: Uuid | null) {
  return useQuery({
    queryKey: queryKeys.study.material(id ?? ''),
    queryFn: () => studyService.getMaterial(id as Uuid),
    enabled: id !== null,
  });
}

function useStudyMutationSideEffects() {
  const client = useQueryClient();
  return async () => {
    await Promise.all([
      client.invalidateQueries({ queryKey: queryKeys.study.all }),
      client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
    ]);
  };
}

export function useCreateStudyMaterial() {
  const onSettled = useStudyMutationSideEffects();
  return useMutation<StudyMaterial, Error, CreateStudyMaterialPayload>({
    mutationFn: (payload) => studyService.createMaterial(payload),
    onSuccess: onSettled,
  });
}

export function useDeleteStudyMaterial() {
  const onSettled = useStudyMutationSideEffects();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => studyService.removeMaterial(id),
    onSuccess: onSettled,
  });
}

/**
 * Генерация конспекта — мутация, а не запрос.
 *
 * Формально это чтение PDF, но операция МЕНЯЕТ состояние сервера: конспект
 * сохраняется в материале, вызов пишется в AIHistory, при достаточной
 * уверенности создаётся рекомендация. Оформить её как useQuery означало бы
 * разрешить React Query повторять её автоматически.
 */
export function useSummarizeMaterial() {
  const client = useQueryClient();
  return useMutation<AiResult<StudySummary>, Error, Uuid>({
    mutationFn: (materialId) => studyService.summarize(materialId),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.study.all }),
        client.invalidateQueries({ queryKey: queryKeys.ai.all }),
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}

// ---- Тесты ---------------------------------------------------------------

export function useGenerateQuiz() {
  const client = useQueryClient();
  return useMutation<AiResult<Quiz>, Error, GenerateQuizPayload>({
    mutationFn: (payload) => studyService.generateQuiz(payload),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.study.all }),
        client.invalidateQueries({ queryKey: queryKeys.ai.all }),
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}

export function useSubmitQuiz() {
  const client = useQueryClient();
  return useMutation<QuizGrade, Error, { quizId: Uuid; answers: number[] }>({
    mutationFn: ({ quizId, answers }) => studyService.submitQuiz(quizId, answers),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.study.all }),
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}

// ---- Заметки -------------------------------------------------------------

export function useStudyNotes(materialId: Uuid | null) {
  return useQuery({
    queryKey: queryKeys.study.notes(materialId ?? ''),
    queryFn: () => studyService.listNotes(materialId as Uuid),
    enabled: materialId !== null,
  });
}

export function useCreateStudyNote() {
  const onSettled = useStudyMutationSideEffects();
  return useMutation<StudyNote, Error, { studyMaterialId: Uuid; content: string }>({
    mutationFn: (payload) => studyService.createNote(payload),
    onSuccess: onSettled,
  });
}

export function useUpdateStudyNote() {
  const onSettled = useStudyMutationSideEffects();
  return useMutation<StudyNote, Error, { id: Uuid; content: string }>({
    mutationFn: ({ id, content }) => studyService.updateNote(id, content),
    onSuccess: onSettled,
  });
}

export function useDeleteStudyNote() {
  const onSettled = useStudyMutationSideEffects();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => studyService.removeNote(id),
    onSuccess: onSettled,
  });
}
