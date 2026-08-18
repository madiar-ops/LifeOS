import { AlertTriangle } from 'lucide-react';

import { Button } from './Button';
import { Modal } from './Modal';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  /** Что именно произойдёт. Пишем последствия, а не «вы уверены?». */
  message: string;
  confirmLabel?: string;
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Подтверждение необратимого действия.
 *
 * Отдельный компонент, а не `window.confirm`: системное окно блокирует поток
 * выполнения, не поддаётся оформлению и не умеет показывать состояние
 * ожидания — а удаление идёт по сети и может занять секунду.
 */
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Удалить',
  loading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Modal
      open={open}
      onClose={onCancel}
      title={title}
      size="sm"
      footer={
        <>
          <Button variant="secondary" onClick={onCancel} disabled={loading}>
            Отмена
          </Button>
          <Button variant="danger" onClick={onConfirm} loading={loading}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <div className="flex gap-3">
        <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-danger-soft text-danger">
          <AlertTriangle size={18} />
        </span>
        <p className="text-sm leading-relaxed text-fg-muted">{message}</p>
      </div>
    </Modal>
  );
}
