"""Локальные алгоритмы обработки текста — запасной путь без LLM.

Суммаризация здесь извлекающая (extractive): выбираются самые
информативные предложения исходного текста. В отличие от генеративной,
она физически не может ничего выдумать — это прямое следствие
требования MASTER_GUIDE «AI никогда не генерирует случайные ответы».
"""

from __future__ import annotations

import re

import numpy as np
from sklearn.feature_extraction.text import TfidfVectorizer

SENTENCE_PATTERN = re.compile(r"(?<=[.!?…])\s+")
WORD_PATTERN = re.compile(r"\b[\w-]{3,}\b", re.UNICODE)


MIN_SENTENCE_LENGTH = 25


def split_sentences(text: str) -> list[str]:
    sentences = [s.strip() for s in SENTENCE_PATTERN.split(text) if s.strip()]

    # Обрывки короче 25 символов обычно мусор: заголовки, номера страниц.
    meaningful = [s for s in sentences if len(s) >= MIN_SENTENCE_LENGTH]

    # Но если отсев не оставил ничего (материал целиком состоит из коротких
    # тезисов — типичный конспект лекции списком), возвращать пустоту нельзя:
    # пользователь получил бы пустой конспект вместо ответа. Откатываемся
    # к неотфильтрованным предложениям — они всё равно из исходного текста.
    return meaningful or sentences


def extractive_summary(text: str, max_sentences: int = 7) -> tuple[str, list[str]]:
    """TF-IDF ранжирование предложений.

    Вес предложения — сумма TF-IDF его слов, нормированная на длину,
    иначе побеждали бы просто самые длинные предложения.
    Порядок исходного текста сохраняется, чтобы конспект читался связно.
    """

    sentences = split_sentences(text)

    if len(sentences) <= max_sentences:
        return " ".join(sentences), sentences

    vectorizer = TfidfVectorizer(max_features=5000, sublinear_tf=True)
    matrix = vectorizer.fit_transform(sentences)

    lengths = np.array([max(len(s.split()), 1) for s in sentences])
    scores = np.asarray(matrix.sum(axis=1)).ravel() / np.sqrt(lengths)

    top_indices = sorted(np.argsort(scores)[-max_sentences:])
    selected = [sentences[i] for i in top_indices]

    return " ".join(selected), selected


def keywords(text: str, limit: int = 15) -> list[str]:
    """Ключевые термины по TF-IDF на уровне слов."""

    sentences = split_sentences(text) or [text]

    try:
        vectorizer = TfidfVectorizer(max_features=2000, sublinear_tf=True)
        matrix = vectorizer.fit_transform(sentences)
    except ValueError:
        return []

    scores = np.asarray(matrix.sum(axis=0)).ravel()
    names = vectorizer.get_feature_names_out()

    top = np.argsort(scores)[-limit:][::-1]
    return [names[i] for i in top if len(names[i]) >= 3]


def word_count(text: str) -> int:
    return len(WORD_PATTERN.findall(text))
