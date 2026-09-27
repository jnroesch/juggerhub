import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { stepMotion } from './step-motion';

describe('stepMotion', () => {
  const order = ['basics', 'type', 'review'] as const;
  type Step = (typeof order)[number];

  const setup = (initial: Step) => {
    const step = signal<Step>(initial);
    const motion = TestBed.runInInjectionContext(() => stepMotion(step, order));
    return { step, motion };
  };

  it('does not animate the step the wizard opens on', () => {
    expect(setup('type').motion()).toBe('');
  });

  it('travels forward to a later step and back to an earlier one', () => {
    const { step, motion } = setup('basics');
    motion();

    step.set('type');
    expect(motion()).toBe('jh-step-forward');

    step.set('basics');
    expect(motion()).toBe('jh-step-back');
  });

  it('travels back on a jump from review to the first step', () => {
    const { step, motion } = setup('review');
    motion();

    step.set('basics');
    expect(motion()).toBe('jh-step-back');
  });
});
