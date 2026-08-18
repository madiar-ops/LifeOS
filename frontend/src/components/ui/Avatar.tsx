import { useState } from 'react';

import { cn } from '@/lib/cn';
import { initials } from '@/lib/format';

interface AvatarProps {
  name: string;
  surname: string;
  url?: string | null;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

const SIZES = {
  sm: 'size-7 text-[11px]',
  md: 'size-9 text-[13px]',
  lg: 'size-20 text-2xl',
};

/**
 * Аватар с запасным вариантом из инициалов.
 *
 * Состояние `broken` нужно, потому что ссылка на файл может стать
 * недействительной: при локальном провайдере хранилища файлы лежат в
 * wwwroot/uploads, а на Render файловая система эфемерна (ADR 42) — после
 * перезапуска URL остаётся в базе, а файла уже нет. Без обработки ошибки
 * загрузки пользователь увидел бы иконку битой картинки.
 */
export function Avatar({ name, surname, url, size = 'md', className }: AvatarProps) {
  const [broken, setBroken] = useState(false);
  const showImage = url !== null && url !== undefined && url !== '' && !broken;

  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full',
        'border border-line bg-accent-soft font-semibold text-accent select-none',
        SIZES[size],
        className,
      )}
    >
      {showImage ? (
        <img
          src={url}
          alt={`${name} ${surname}`}
          className="size-full object-cover"
          onError={() => setBroken(true)}
        />
      ) : (
        <span aria-hidden="true">{initials(name, surname)}</span>
      )}
    </span>
  );
}
