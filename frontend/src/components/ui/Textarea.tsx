import type { ComponentPropsWithRef } from 'react';

import { cn } from '@/lib/cn';

import { controlClasses } from './controlClasses';

export function Textarea({ className, rows = 4, ...rest }: ComponentPropsWithRef<'textarea'>) {
  return <textarea rows={rows} className={cn(controlClasses, 'py-2 leading-relaxed', className)} {...rest} />;
}
