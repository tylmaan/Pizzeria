import { type CategoryDto } from '@pizzeria/api-client';
import { useTranslation } from '@pizzeria/i18n';
import { Alert, Button, buttonClassName, TextField } from '@pizzeria/ui';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { PageHeader } from '../company/PageHeader';
import { useFormErrors } from '../lib/formErrors';
import { MenuEntityLoader } from './MenuEntityLoader';
import { useSaveCategory } from './queries';

const menuPath = '/company/menu';

export function NewCategoryPage() {
  return <CategoryForm />;
}

export function EditCategoryPage() {
  const { id = '' } = useParams();
  return (
    <MenuEntityLoader find={(menu) => menu.categories.find((c) => c.id === id)}>
      {(category) => <CategoryForm key={category.id} existing={category} />}
    </MenuEntityLoader>
  );
}

type SizeRow = { key: string; id: string | null; name: string };

let nextKey = 0;
const newRow = (id: string | null = null, name = ''): SizeRow => ({ key: `size-${nextKey++}`, id, name });

function CategoryForm({ existing }: { existing?: CategoryDto }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const save = useSaveCategory(existing?.id);
  const errors = useFormErrors(save.error);

  const [name, setName] = useState(existing?.name ?? '');
  const [sizes, setSizes] = useState<SizeRow[]>(
    existing ? existing.sizes.map((s) => newRow(s.id, s.name)) : [newRow()],
  );

  const updateSize = (key: string, value: string) =>
    setSizes((rows) => rows.map((row) => (row.key === key ? { ...row, name: value } : row)));

  const submit = (event: FormEvent) => {
    event.preventDefault();
    save.mutate(
      { name, sizes: sizes.map((s) => ({ id: s.id, name: s.name })) },
      { onSuccess: () => void navigate(menuPath) },
    );
  };

  return (
    <section className="max-w-xl">
      <PageHeader title={existing ? t('menu.editCategoryTitle') : t('menu.newCategoryTitle')} />
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {errors.general && <Alert>{errors.general}</Alert>}
        <TextField
          label={t('menu.categoryName')}
          value={name}
          onChange={(e) => setName(e.target.value)}
          error={errors.field('name')}
          autoFocus
        />
        <fieldset className="flex flex-col gap-3">
          <legend className="text-base font-semibold">{t('menu.sizes')}</legend>
          <p className="text-sm text-neutral-600">{t('menu.sizesHint')}</p>
          {sizes.map((size, index) => (
            <div key={size.key} className="flex items-end gap-2">
              <div className="flex-1">
                <TextField
                  label={t('menu.sizeName', { number: index + 1 })}
                  value={size.name}
                  onChange={(e) => updateSize(size.key, e.target.value)}
                  error={errors.field(`sizes[${index}].name`)}
                />
              </div>
              <Button
                variant="secondary"
                disabled={sizes.length === 1}
                onClick={() => setSizes((rows) => rows.filter((row) => row.key !== size.key))}
              >
                {t('common.remove')}
              </Button>
            </div>
          ))}
          <Button variant="secondary" className="self-start" onClick={() => setSizes((rows) => [...rows, newRow()])}>
            {t('menu.addSize')}
          </Button>
        </fieldset>
        <div className="flex gap-3">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
          <Link to={menuPath} className={buttonClassName('secondary')}>
            {t('common.cancel')}
          </Link>
        </div>
      </form>
    </section>
  );
}
