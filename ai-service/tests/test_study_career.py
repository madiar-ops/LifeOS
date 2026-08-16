"""Учебный и карьерный модули (работают без ключа LLM)."""

SAMPLE_TEXT = (
    "Машинное обучение это раздел искусственного интеллекта. "
    "Алгоритмы обучаются на данных и выявляют закономерности. "
    "Существует обучение с учителем и обучение без учителя. "
    "При обучении с учителем модель получает размеченные примеры. "
    "Без учителя алгоритм самостоятельно находит структуру в данных. "
    "Качество модели оценивают на отложенной выборке. "
    "Переобучение возникает когда модель запоминает обучающие данные. "
    "Регуляризация помогает бороться с переобучением."
)


def test_extractive_summary_without_llm(client, auth_headers):
    """Без ключа LLM конспект должен строиться локально, а не падать."""
    response = client.post(
        "/study/summary",
        headers=auth_headers,
        json={"text": SAMPLE_TEXT, "max_sentences": 3},
    )
    assert response.status_code == 200

    payload = response.json()
    assert payload["result"]["source"] == "extractive"
    assert payload["result"]["summary"]


def test_extractive_summary_does_not_invent_text(client, auth_headers):
    """Извлекающий метод физически не может выдумать содержание —
    каждое предложение конспекта обязано присутствовать в оригинале."""
    payload = client.post(
        "/study/summary",
        headers=auth_headers,
        json={"text": SAMPLE_TEXT, "max_sentences": 3},
    ).json()

    for point in payload["result"]["key_points"]:
        assert point.strip() in SAMPLE_TEXT


def test_quiz_admits_unavailability(client, auth_headers):
    """Без LLM сервис обязан честно сказать, что не может составить тест,
    а не выдать бессмысленные вопросы."""
    payload = client.post(
        "/study/quiz", headers=auth_headers, json={"text": SAMPLE_TEXT, "question_count": 3}
    ).json()

    assert payload["result"]["source"] == "unavailable"
    assert payload["is_confident"] is False
    assert payload["result"]["questions"] == []


def test_resume_detects_missing_skills(client, auth_headers):
    payload = client.post(
        "/career/resume-analysis",
        headers=auth_headers,
        json={
            "resume_text": (
                "Опыт работы: разработчик 3 года. Образование: университет. "
                "Навыки: C#, ASP.NET Core, React. Сократил время отклика на 40% "
                "в проекте для команды из 10 человек."
            ),
            "desired_position": "Backend Developer",
            "skills": ["C#", "Docker", "Kubernetes"],
        },
    ).json()

    assert "Docker" in payload["result"]["missing_skills"]
    assert "Kubernetes" in payload["result"]["missing_skills"]
    assert "C#" not in payload["result"]["missing_skills"]


def test_heuristic_confidence_is_lower(client, auth_headers):
    """Эвристика слабее LLM — уверенность обязана это отражать."""
    payload = client.post(
        "/career/resume-analysis",
        headers=auth_headers,
        json={"resume_text": "Опыт работы разработчиком три года в компании." * 5},
    ).json()

    assert payload["result"]["source"] == "heuristic"
    assert payload["confidence"] < 0.8
