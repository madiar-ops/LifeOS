import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { fileService } from '@/services/fileService';
import type { FileQuery, StoredFile, Uuid } from '@/types/api';
import type { ModuleType } from '@/types/enums';

import { queryKeys } from './queryKeys';

export function useFiles(query: FileQuery) {
  return useQuery({
    queryKey: queryKeys.files.list(query),
    queryFn: () => fileService.list(query),
    placeholderData: keepPreviousData,
  });
}

export function useUploadFile() {
  const client = useQueryClient();
  return useMutation<
    StoredFile,
    Error,
    { file: File; module: ModuleType; onProgress?: (percent: number) => void }
  >({
    mutationFn: ({ file, module, onProgress }) => fileService.upload(file, module, onProgress),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.files.all }),
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}

export function useDeleteFile() {
  const client = useQueryClient();
  return useMutation<void, Error, Uuid>({
    mutationFn: (id) => fileService.remove(id),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: queryKeys.files.all }),
        client.invalidateQueries({ queryKey: queryKeys.dashboard.all }),
      ]);
    },
  });
}
