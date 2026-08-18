import { Check, RotateCcw, X } from 'lucide-react';
import { useMemo, useState } from 'react';

import { Badge, Button, Card, CardBody, CardHeader, ProgressBar } from '@/components/ui';
import { useSubmitQuiz } from '@/hooks/useStudy';
import { cn } from '@/lib/cn';
import { formatPercent } from '@/lib/format';
import { toast } from '@/lib/toastBus';
import type { Quiz, QuizGrade } from '@/types/api';

interface QuizRunnerProps {
  quiz: Quiz;
  onClose: () => void;
}

/**
 * Прохождение теста.
 *
 * КЛЮЧЕВОЕ АРХИТЕКТУРНОЕ СВОЙСТВО: правильных ответов у клиента НЕТ.
 * `QuizQuestionResponse` их не содержит (ADR 72), поэтому подсветить верный
 * вариант до отправки физически невозможно — тест нельзя решить через DevTools.
 * Оценка приходит с сервера в `QuizGrade`, и только оттуда становится известен
 * `correctIndex` каждого вопроса.
 *
 * Это не ограничение интерфейса, а причина, по которой результату можно верить.
 */
export function QuizRunner({ quiz, onClose }: QuizRunnerProps) {
  // `null` = вопрос без ответа. Индекс 0 — валидный ответ, поэтому проверять
  // на «ложность» нельзя, только на null.
  const [answers, setAnswers] = useState<(number | null)[]>(() =>
    Array.from({ length: quiz.questions.length }, () => null),
  );
  const [grade, setGrade] = useState<QuizGrade | null>(null);
  const submitQuiz = useSubmitQuiz();

  const answeredCount = answers.filter((answer) => answer !== null).length;
  const allAnswered = answeredCount === quiz.questions.length;

  /** Результат по индексу вопроса — для подсветки после проверки. */
  const resultByIndex = useMemo(() => {
    const map = new Map<number, QuizGrade['results'][number]>();
    for (const result of grade?.results ?? []) map.set(result.questionIndex, result);
    return map;
  }, [grade]);

  const submit = async () => {
    if (!allAnswered) return;
    try {
      // Бэкенд требует ровно столько ответов, сколько вопросов, — иначе 400.
      const payload = answers.map((answer) => answer ?? 0);
      const result = await submitQuiz.mutateAsync({ quizId: quiz.id, answers: payload });
      setGrade(result);
      toast.success(
        'Тест проверен',
        `Верно ${String(result.score)} из ${String(result.totalQuestions)}.`,
      );
    } catch {
      /* уведомление показал глобальный обработчик */
    }
  };

  const reset = () => {
    setGrade(null);
    setAnswers(Array.from({ length: quiz.questions.length }, () => null));
  };

  return (
    <Card>
      <CardHeader
        title="Тест по материалу"
        description={
          grade === null
            ? `Отвечено ${String(answeredCount)} из ${String(quiz.questions.length)}`
            : `Результат: ${String(grade.score)} из ${String(grade.totalQuestions)}`
        }
        actions={
          grade === null ? (
            <Button variant="primary" size="sm" loading={submitQuiz.isPending} onClick={() => void submit()} disabled={!allAnswered}>
              Проверить
            </Button>
          ) : (
            <div className="flex gap-2">
              <Button variant="secondary" size="sm" leftIcon={<RotateCcw size={14} />} onClick={reset}>
                Пройти заново
              </Button>
              <Button variant="ghost" size="sm" onClick={onClose}>
                Закрыть
              </Button>
            </div>
          )
        }
      />

      <CardBody className="space-y-5">
        {grade !== null && (
          <div className="space-y-2">
            <div className="flex items-baseline justify-between">
              <span className="text-[13px] text-fg-muted">Доля верных ответов</span>
              <span className="tabular text-sm font-semibold">
                {formatPercent((grade.score / grade.totalQuestions) * 100)}
              </span>
            </div>
            <ProgressBar
              value={(grade.score / grade.totalQuestions) * 100}
              tone={grade.score / grade.totalQuestions >= 0.6 ? 'success' : 'danger'}
              label="Доля верных ответов"
            />
          </div>
        )}

        <ol className="space-y-5">
          {quiz.questions.map((question, questionIndex) => {
            const result = resultByIndex.get(questionIndex);
            const selected = answers[questionIndex] ?? null;

            return (
              <li key={`${String(questionIndex)}-${question.question}`} className="space-y-2.5">
                <div className="flex items-start gap-2">
                  <span className="tabular mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-md bg-surface-3 text-[11px] font-semibold text-fg-muted">
                    {questionIndex + 1}
                  </span>
                  <p className="text-[13.5px] leading-relaxed font-medium text-fg">
                    {question.question}
                  </p>
                  {result !== undefined && (
                    <Badge tone={result.isCorrect ? 'success' : 'danger'} className="ml-auto shrink-0">
                      {result.isCorrect ? 'верно' : 'неверно'}
                    </Badge>
                  )}
                </div>

                <div className="space-y-1.5 pl-7">
                  {question.options.map((option, optionIndex) => {
                    const isSelected = selected === optionIndex;
                    // До проверки корректность неизвестна — сервер её не присылал.
                    const isCorrect = result !== undefined && result.correctIndex === optionIndex;
                    const isWrongPick =
                      result !== undefined && result.submittedIndex === optionIndex && !result.isCorrect;

                    return (
                      <label
                        key={`${String(optionIndex)}-${option}`}
                        className={cn(
                          'flex cursor-pointer items-start gap-2.5 rounded-lg border px-3 py-2 text-[13px]',
                          'transition-colors duration-150',
                          isCorrect
                            ? 'border-success/45 bg-success-soft'
                            : isWrongPick
                              ? 'border-danger/45 bg-danger-soft'
                              : isSelected
                                ? 'border-accent bg-accent-soft'
                                : 'border-line hover:border-line-strong hover:bg-surface-2',
                          grade !== null && 'cursor-default',
                        )}
                      >
                        <input
                          type="radio"
                          name={`question-${String(questionIndex)}`}
                          checked={isSelected}
                          disabled={grade !== null}
                          onChange={() =>
                            setAnswers((current) =>
                              current.map((value, index) =>
                                index === questionIndex ? optionIndex : value,
                              ),
                            )
                          }
                          className="sr-only"
                        />
                        <span
                          aria-hidden="true"
                          className={cn(
                            'mt-0.5 flex size-4 shrink-0 items-center justify-center rounded-full border',
                            isCorrect
                              ? 'border-success bg-success text-white'
                              : isWrongPick
                                ? 'border-danger bg-danger text-white'
                                : isSelected
                                  ? 'border-accent bg-accent'
                                  : 'border-line-strong',
                          )}
                        >
                          {isCorrect && <Check size={10} strokeWidth={3.5} />}
                          {isWrongPick && <X size={10} strokeWidth={3.5} />}
                        </span>
                        <span className="text-fg">{option}</span>
                      </label>
                    );
                  })}
                </div>

                {/* Объяснение показывается ТОЛЬКО после проверки: до неё оно
                    подсказывало бы правильный ответ. */}
                {result !== undefined && result.explanation !== '' && (
                  <p className="ml-7 rounded-lg bg-surface-2 px-3 py-2 text-[12.5px] leading-relaxed text-fg-muted">
                    {result.explanation}
                  </p>
                )}
              </li>
            );
          })}
        </ol>
      </CardBody>
    </Card>
  );
}
