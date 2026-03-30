# Аналізатор Постів Reddit 🚀

Сервіс Web API для аналізуsubreddit-ів, фільтрації постів за ключовими словами та виявлення медіаконтенту.

## Функціональні можливості
- ✅ **Аналіз декількох subreddit-ів**: Обробка списку за один запит.
- ✅ **Фільтрація за ключовими словами**: Пошук у заголовках та тексті (body) поста.
- ✅ **Виявлення зображень**: Визначення, чи містить пост зображення.
- ✅ **Експорт у JSON**: Завантаження результатів у вигляді структурованого файлу.
- ✅ **Багатопоточність**: Використання `Task.WhenAll` для паралельного збору даних.
- ✅ **Логування**: Запис активності та помилок у файл `out.log`.
- ✅ **Docker**: Повністю контейнеризований проект із multi-stage build.

---

## 🛠 Встановлення та запуск

### Попередні вимоги
- Встановлений та запущений [Docker Desktop](https://www.docker.com/products/docker-desktop/).

### Швидкий старт (через Docker)
1. Клонуйте репозиторій.
2. Відкрийте термінал у кореневій папці проекту.
3. Виконайте команду:
   ```bash
   docker-compose up --build
   ```
4. Перейдіть за адресою: **[http://localhost:8080](http://localhost:8080)**

---

## 📖 Інструкція використання

### Веб-інтерфейс (UI)
Сервіс надає зручну панель керування:
1. Додавайте/видаляйте subreddit-и.
2. Вводьте ключові слова через кому.
3. Встановлюйте ліміт (N) постів для кожного subreddit-у.
4. Завантажуйте результати кнопкою "Download JSON".

---

## 📡 Опис API

### POST `/api/reddit`
Аналізує пости та повертає результат у форматі JSON.

**Приклад тіла запиту (Request Body):**
```json
{
  "items": [
    {
      "subreddit": "r/nature",
      "keywords": ["forest", "river"]
    },
    {
      "subreddit": "r/aww",
      "keywords": ["cat", "dog"]
    }
  ],
  "limit": 25
}
```

**Приклад відповіді (Default Response):**
```json
{
    "subreddits": {
        "r/nature": {
            "posts": [
                {
                    "title": "In the drought-strained Mountain West, a New Mexico river offers a glimpse of resilience, study finds",
                    "hasImage": false,
                    "imageUrl": "",
                    "postUrl": "https://www.reddit.com/r/nature/comments/1s7nve8/in_the_droughtstrained_mountain_west_a_new_mexico/"
                }
            ],
            "count": 1
        },
        "r/aww": {
            "posts": [
                {
                    "title": "My cat just woke up and can\u2019t stop yawning \u0026amp; lip-smacking\u2026 the sleepiness is too pure \uD83E\uDD79",
                    "hasImage": false,
                    "imageUrl": "",
                    "postUrl": "https://www.reddit.com/r/aww/comments/1s7eskb/my_cat_just_woke_up_and_cant_stop_yawning/"
                },
                {
                    "title": "My dog is so gentle with our rowdy kitten",
                    "hasImage": false,
                    "imageUrl": "",
                    "postUrl": "https://www.reddit.com/r/aww/comments/1s7h2aq/my_dog_is_so_gentle_with_our_rowdy_kitten/"
                },
                {
                    "title": "I got a new kitty cat.",
                    "hasImage": true,
                    "imageUrl": "https://i.redd.it/dyavn63rm3sg1.jpeg",
                    "postUrl": "https://www.reddit.com/r/aww/comments/1s7f6z3/i_got_a_new_kitty_cat/"
                },
                {
                    "title": "A while ago I did a cherry blossom photoshoot with my dog. It turned out even better than I could\u2019ve imagined!",
                    "hasImage": true,
                    "imageUrl": "https://i.redd.it/84z1y2xl24sg1.jpeg",
                    "postUrl": "https://www.reddit.com/r/aww/comments/1s7gwja/a_while_ago_i_did_a_cherry_blossom_photoshoot/"
                }
            ],
            "count": 4
        }
    }
}
```

---

## 🧠 Теоретичне питання

### Питання: Які проблеми можуть виникнути при такому підході до отримання даних із веб-сторінок (через HTTP + парсинг HTML)? Як би ви їх вирішували?

#### Проблеми:
1. **Динамічний контент (SPA)**: Сучасні сайти (як Reddit) часто рендерять контент через JavaScript після завантаження сторінки. Звичайний HTTP-запит отримає лише пустий каркас HTML без постів.
2. **Блокування та анти-бот системи**: Сайти відстежують частоту запитів з однієї IP-адреси, перевіряють User-Agent та наявність кук. Це може призвести до капчі або повного блокування.
3. **Крихкість структури (Fragility)**: HTML-код часто змінюється. Селектор, який працював сьогодні, може зламатися завтра після оновлення дизайну сайту.
4. **Продуктивність**: Парсинг великих обсягів HTML-тексту займає багато ресурсів CPU порівняно з роботою через API.

#### Рішення:
1. **Приховані API**: Завжди краще шукати внутрішні API-інтерфейси (наприклад, суфікс `.json` у Reddit). Це стабільно і швидше.
2. **Headless Browsers**: Використання **Playwright** або **Selenium**, якщо необхідно виконати JS-скрипти для відображення контенту.
3. **Ротація IP та проксі**: Використання пулу проксі-серверів та підміна заголовків (User-Agents), щоб імітувати поведінку реальних користувачів.
4. **Rate Limiting (Затримки)**: Додавання випадкових пауз (jitter) між запитами для запобігання спрацюванню захисту від ботів.

---

## 📄 Ліцензія
MIT
