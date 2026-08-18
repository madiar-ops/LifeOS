import { api } from '@/lib/httpClient';
import type {
  AiResult,
  CreateStudyMaterialPayload,
  CreateStudyNotePayload,
  GenerateQuizPayload,
  PagedResponse,
  PaginationParams,
  Quiz,
  QuizGrade,
  StudyMaterial,
  StudyNote,
  StudySummary,
  Uuid,
} from '@/types/api';

/** Учёба: `StudyController`. */
export const studyService = {
  // ---- Материалы --------------------------------------------------------

  listMaterials(query: PaginationParams): Promise<PagedResponse<StudyMaterial>> {
    return api.get<PagedResponse<StudyMaterial>>('/study/materials', query);
  },

  getMaterial(id: Uuid): Promise<StudyMaterial> {
    return api.get<StudyMaterial>(`/study/materials/${id}`);
  },

  /**
   * Создание материала из УЖЕ загруженного файла.
   *
   * Двухшаговый процесс (сначала /files/upload, потом /study/materials) —
   * это контракт бэкенда, а не неудобство: валидация файла живёт в одном месте
   * и не дублируется в каждом модуле (ADR 71). Один файл = один материал,
   * повторная попытка даёт 409.
   */
  createMaterial(payload: CreateStudyMaterialPayload): Promise<StudyMaterial> {
    return api.post<StudyMaterial>('/study/materials', payload);
  },

  /** Удаление материала. Заметки и тесты уходят каскадом, файл остаётся. */
  removeMaterial(id: Uuid): Promise<void> {
    return api.delete(`/study/materials/${id}`);
  },

  /**
   * Генерация конспекта.
   *
   * Может вернуть 400 `study.no_text_layer`, если PDF — скан без текстового
   * слоя (ADR 70). Поле `result.source` показывает, отработала ли LLM или
   * запасной извлекающий алгоритм — это стоит показать пользователю честно.
   */
  summarize(materialId: Uuid): Promise<AiResult<StudySummary>> {
    return api.post<AiResult<StudySummary>>(`/study/materials/${materialId}/summarize`);
  },

  // ---- Тесты ------------------------------------------------------------

  /**
   * Генерация теста. Требует ключа LLM в ai-service.
   *
   * Без ключа бэкенд отвечает 400 `study.quiz_unavailable` — отказ вместо
   * бессмысленных вопросов (ADR 61). Интерфейс объясняет причину, а не
   * показывает «что-то пошло не так».
   */
  generateQuiz(payload: GenerateQuizPayload): Promise<AiResult<Quiz>> {
    return api.post<AiResult<Quiz>>('/study/quizzes', payload);
  },

  /** Тест по Id. Правильные ответы в ответе отсутствуют (ADR 72). */
  getQuiz(id: Uuid): Promise<Quiz> {
    return api.get<Quiz>(`/study/quizzes/${id}`);
  },

  /** Отправка ответов. Оценка считается на сервере, клиент её не вычисляет. */
  submitQuiz(id: Uuid, answers: number[]): Promise<QuizGrade> {
    return api.post<QuizGrade>(`/study/quizzes/${id}/submit`, { answers });
  },

  // ---- Заметки ----------------------------------------------------------

  /** Заметки материала. Не пагинированы — бэкенд отдаёт весь список. */
  listNotes(materialId: Uuid): Promise<StudyNote[]> {
    return api.get<StudyNote[]>(`/study/materials/${materialId}/notes`);
  },

  createNote(payload: CreateStudyNotePayload): Promise<StudyNote> {
    return api.post<StudyNote>('/study/notes', payload);
  },

  updateNote(id: Uuid, content: string): Promise<StudyNote> {
    return api.put<StudyNote>(`/study/notes/${id}`, { content });
  },

  removeNote(id: Uuid): Promise<void> {
    return api.delete(`/study/notes/${id}`);
  },
};
